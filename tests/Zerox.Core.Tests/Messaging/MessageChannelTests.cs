using System.Runtime.CompilerServices;
using Zerox.Core.Editing;
using Zerox.Core.Identity;
using Zerox.Core.Messaging;

namespace Zerox.Core.Tests.Messaging;

public sealed class MessageChannelTests
{
    internal enum MessageKind
    {
        Name,
        Other,
    }

    internal sealed class MsgDataStruct
    {
        public string Name = string.Empty;
    }

    internal sealed class MsgData : PocoProxy<MsgDataStruct>
    {
        public string Name
        {
            get => Data.Name;
            set => SetProperty(ref Data.Name, value);
        }
    }

    internal sealed class MsgSource : IMessageSource<MessageKind>
    {
        public HierId SourceId { get; } = HierId.Parse("SomeHierId/src");

        public MsgData Data { get; } = new() { Name = "Pippo" };

        public Task<Message<MessageKind>> ProduceMessageAsync(CancellationToken cancellationToken)
            => Task.FromResult(new Message<MessageKind>(MessageKind.Name, SourceId, Data));
    }

    internal sealed class MsgDestination : IMessageDestination<MessageKind>
    {
        public HierId SourceId { get; } = HierId.Parse("SomeHierId/dest");

        public MsgData? Data { get; private set; }

        public bool Accepts { get; set; } = true;

        public int Accepted { get; private set; }

        public Exception? Failure { get; set; }

        public bool CanAccept(Message<MessageKind> message)
            => Accepts && message.Kind == MessageKind.Name && message.SourceId.GetSimilarRootSize(SourceId) == 1;

        public Task AcceptAsync(Message<MessageKind> message, CancellationToken cancellationToken)
        {
            if (Failure is not null)
                throw Failure;
            Accepted++;
            if (message.Data is MsgData data)
                Data = data;
            return Task.CompletedTask;
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AttachAThrowAwayDestination(MessageChannel<MessageKind> channel) => channel.AttachDestination(new MsgDestination());

    [Fact]
    public async Task Test_ChannelP2P_attached()
    {
        var mch = new MessageChannel<MessageKind>("ch1");
        Assert.Null(mch.Source);
        Assert.Null(mch.Destination);
        Assert.False(mch.HasMessage);
        Assert.False(mch.DestinationCanAccept);

        var ms = new MsgSource();
        var md = new MsgDestination();
        mch.AttachSource(ms);
        mch.AttachDestination(md);
        Assert.NotNull(mch.Source);
        Assert.NotNull(mch.Destination);
        Assert.False(mch.HasMessage);
        Assert.False(mch.DestinationCanAccept);

        ms.Data.Name = "pippo & pappo";
        Assert.True(await mch.ReceiveAsync(Token));
        Assert.True(mch.HasMessage);
        Assert.True(mch.DestinationCanAccept);

        mch.DetachDestination();
        Assert.False(mch.DestinationCanAccept);
        Assert.False(await mch.SendAsync(Token));

        mch.AttachDestination(md);
        Assert.True(mch.DestinationCanAccept);
        Assert.True(await mch.SendAsync(Token));
        Assert.Equal("pippo & pappo", md.Data?.Name);
    }

    [Fact]
    public async Task Should_ReportNotReceived_WhenThereIsNoSource()
    {
        var channel = new MessageChannel<MessageKind>("empty");

        (await channel.ReceiveAsync(Token)).Should().BeFalse();
        channel.HasMessage.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReportNotSent_WhenTheDestinationRefuses()
    {
        var channel = new MessageChannel<MessageKind>("refused");
        var source = new MsgSource();
        var destination = new MsgDestination { Accepts = false };
        channel.AttachSource(source);
        channel.AttachDestination(destination);
        await channel.ReceiveAsync(Token);

        (await channel.SendAsync(Token)).Should().BeFalse();

        destination.Accepted.Should().Be(0);
        channel.HasMessage.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ClearAfterSending_WhenSendAndClear()
    {
        var channel = new MessageChannel<MessageKind>("clear");
        var source = new MsgSource();
        var destination = new MsgDestination();
        channel.AttachSource(source);
        channel.AttachDestination(destination);

        await channel.ReceiveAsync(Token);
        (await channel.SendAndClearAsync(Token)).Should().BeTrue();
        channel.HasMessage.Should().BeFalse();

        channel.DetachDestination();
        await channel.ReceiveAsync(Token);
        (await channel.SendAndClearAsync(Token)).Should().BeFalse();
        channel.HasMessage.Should().BeFalse();
    }

    [Fact]
    public async Task Should_NotifyTheMessageProperties_WhenAMessageArrives()
    {
        var channel = new MessageChannel<MessageKind>("notify");
        var source = new MsgSource();
        var destination = new MsgDestination();
        channel.AttachSource(source);
        var names = new List<string?>();
        channel.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        await channel.ReceiveAsync(Token);
        names.Should().Equal("CurrentMessage", "HasMessage", "DestinationCanAccept");

        names.Clear();
        channel.AttachDestination(destination);
        names.Should().Equal("DestinationCanAccept");
    }

    [Fact]
    public void Should_ForgetTheDestination_WhenItIsCollected()
    {
        var channel = new MessageChannel<MessageKind>("weak");
        AttachAThrowAwayDestination(channel);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        channel.Destination.Should().BeNull();
    }

    [Fact]
    public void Should_CompareMessagesById_WhenTwoHaveTheSameContent()
    {
        var source = HierId.Parse("a/b");
        var first = new Message<MessageKind>(MessageKind.Other, source, "data");
        var second = new Message<MessageKind>(MessageKind.Other, source, "data");

        first.Should().NotBe(second);
        first.Equals(first).Should().BeTrue();
    }

    [Fact]
    public async Task Should_LetTheDestinationExceptionReachTheCaller_WhenAcceptFails()
    {
        var channel = new MessageChannel<MessageKind>("failing");
        channel.AttachSource(new MsgSource());
        var destination = new MsgDestination { Failure = new InvalidOperationException("broken node") };
        channel.AttachDestination(destination);
        await channel.ReceiveAsync(Token);

        await FluentActions.Awaiting(() => channel.SendAsync(Token)).Should().ThrowAsync<InvalidOperationException>().WithMessage("broken node");
    }
}
