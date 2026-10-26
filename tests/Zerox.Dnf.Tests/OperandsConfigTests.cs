
namespace Zerox.Dnf.Tests;

public class OperandsConfigTests
{
    [Theory]
    [InlineData("C1.F0", true, 1, 0)]
    [InlineData("C2.F10", true, 2, 10)]
    [InlineData("C4.F15", true, 4, 15)]
    [InlineData("C5.F0", false, 0, 0)]   // Invalid camera
    [InlineData("C1.F16", false, 0, 0)]  // Invalid filter
    [InlineData("X1.F0", false, 0, 0)]   // Wrong prefix
    [InlineData("C1F0", false, 0, 0)]    // Missing dot
    [InlineData("", false, 0, 0)]        // Empty
    public void TryParseBitRef_ParsesCorrectly(string operand, bool expectedResult, int expectedCam, int expectedFilt)
    {
        bool result = OperandsConfig.TryParseBitRef(operand, out int cam, out int filt);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedCam, cam);
            Assert.Equal(expectedFilt, filt);
        }
    }
}
