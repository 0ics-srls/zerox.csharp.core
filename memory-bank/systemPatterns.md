§MBEL:5.0

# Architecture

[PROJECTS]
@Zerox.Core::no UI;areas Async,Cancellation,Collections,Statistics,Messaging,Reflection,Text,Numbers,Identity,Validation,Editing
@Zerox.Core.Avalonia::Avalonia 12.1.4;references Zerox.Core;PropertyContainer + shared throttle pump,async commands
@Zerox.Dnf::boolean expression → DNF masks → compiled rules
@Zerox.Ef.*::the database → API foundation,a family of four:Ef.Core (contracts,no deps) → Ef.Db (IDbContext + EF:session interceptor,repository,unit of work,massive op,optimistic lock) → Ef.Domain (DevExtreme search,validator,distinct values,last writer wins) → Ef.Api (EntityController,errors,JSON as is);folders src/Zerox.Ef/{Core,Db,Domain,Api} + tests/Zerox.Ef/{Db,Domain,Api} (©User 2026-10-27:one folder per namespace segment);example in tests/Zerox.Ef/Api/Sample
@tests::tests/<Project>.Tests per project (xunit v3 + AwesomeAssertions on MTP);Avalonia tests through HeadlessUnitTestSession

[RULES]
!KISS::no interface without a consumer that substitutes it;no registration method without something to wire;no base applied by dogma
!DRY::one implementation per capability (binary search on ReadOnlySpan,one circular buffer,one change notification,one JSON)
!SOLID::S one actor per class;L no deep hierarchies,composition,no NotImplemented subclasses;I small interfaces seen by the user;D abstraction owned by its consumer;O only on real axes of change
!P9::expected failure = Try-pattern or result;misuse = ArgumentException;unexpected exception never caught to go on;web exception (©User 2026-10-27 "processo non può morire,exception devono passare da middleware"):every exception of a request ends in the error middleware (Error log + 500,client gone = Information,response started = connection aborted),a web process never dies
!optimistic lock::©User 2026-10-27 "gestito in maniera corretta":client Version given → save only at that Version,stale = 409,never retried (PUT,rename via LastWriterWins,DELETE ?version=N required on lock entities);retry + last writer wins only without a client Version
!async-only::©User 2026-10-27 "zerox ha roba sincrona…rimuovi tutto se non strettamente necessario,Zerox.Ef deve essere 0 sync code":no sync twin of an async method,no blocking wait,work = Func<CancellationToken,Task> (JobScheduler jobs + TickAsync,WorkingProcess cycle);lock only around short in-memory state;Zerox.Ef session refuses every sync road to the database (LinqQuery,SaveChanges,FromSql,ExecuteSqlRaw,ExecuteUpdate/Delete,connection open,transaction start/commit/rollback) → SyncDatabaseAccessException;EF's own schema work (EnsureCreatedAsync/MigrateAsync run sync commands inside) left alone by CommandSource
!DI::constructor injection,no container inside components
!visibility::internal sealed by default,public is a decision
