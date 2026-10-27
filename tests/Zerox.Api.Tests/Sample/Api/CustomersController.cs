using Zerox.Api.Tests.Sample.Db;
using Zerox.Core.Data;

namespace Zerox.Api.Tests.Sample.Api;

public sealed class CustomersController(IRepository<Customer> repository) : EntityController<Customer>(repository);
