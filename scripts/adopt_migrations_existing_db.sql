-- One-time step for a database that was created BEFORE EF migrations
-- (e.g. the original Product6Pos dev database).
--
-- It records the "Baseline" migration as already applied — the tables it
-- describes already exist — so `dotnet ef database update` then applies
-- only the newer migrations (OnlineReadiness, ...).
--
-- Do NOT run this on a fresh/empty database; there just run
-- `dotnet ef database update` and every migration is applied normally.

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF OBJECT_ID(N'[SalesBills]') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] LIKE N'%[_]Baseline')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101171249_Baseline', N'10.0.9');
END;
