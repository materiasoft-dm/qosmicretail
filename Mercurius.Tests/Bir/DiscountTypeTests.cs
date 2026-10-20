using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Controllers.Api;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Mercurius.Shared.Bir;
using Mercurius.Shared.Common;
using Mercurius.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mercurius.Tests.Bir
{
    /// <summary>Settings → Discount Types, its sync to the till, and the migration that seeds the statutory ones.</summary>
    public class DiscountTypeTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MercuriusDbContext _context;

        public DiscountTypeTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<MercuriusDbContext>().UseSqlite(_connection).Options;
            _context = new MercuriusDbContext(options, TestTenantContext.Instance);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task SeedMigration_AddsTheStatutoryDiscountsToEveryTenantOnce()
        {
            var migrator = _context.GetService<IMigrator>();
            await migrator.MigrateAsync("AddBirPosAccreditation");

            _context.Tenants.AddRange(
                new Tenant { Id = 1, Name = "Botika ni Juan", CreatedDate = DateTime.UtcNow },
                new Tenant { Id = 2, Name = "Farmacia Maria", CreatedDate = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            // Tenant 2 already set up its own senior discount: it must not get a second one.
            _context.DiscountTypes.Add(new DiscountType { TenantId = 2, Name = "Lolo/Lola", Value = 20m, Category = (int)SpecialDiscountType.SeniorCitizen });
            await _context.SaveChangesAsync();

            await migrator.MigrateAsync();

            var rows = await _context.DiscountTypes.IgnoreQueryFilters().AsNoTracking().ToListAsync();
            foreach (var tenantId in new[] { 1, 2 })
            {
                var mine = rows.Where(r => r.TenantId == tenantId).ToList();
                Assert.Equal(5, mine.Count);
                Assert.Equal(new[] { 1, 2, 3, 4, 5 }, mine.Select(r => r.Category).OrderBy(c => c));
            }
            Assert.Equal("Lolo/Lola", rows.Single(r => r.TenantId == 2 && r.Category == 1).Name);

            var pwd = rows.Single(r => r.TenantId == 1 && r.Category == (int)SpecialDiscountType.Pwd);
            Assert.Equal(20m, pwd.Value);
            Assert.Equal((int)DiscountValueKind.Percent, pwd.ValueKind);
            Assert.True(pwd.IsVatExempt && pwd.RequiresCustomerId && pwd.IsActive);
            Assert.False(rows.Single(r => r.TenantId == 1 && r.Category == (int)SpecialDiscountType.MedalOfValor).IsVatExempt);
        }

        [Fact]
        public async Task AdminCanAddAFixedAmountDiscount_AndOnlyActiveOnesReachTheTill()
        {
            _context.Database.EnsureCreated();
            var unitOfWork = new EfUnitOfWork(_context, TestTenantContext.Instance);
            foreach (var d in DiscountTypeDefaults.For(TestTenantContext.DefaultTenantId))
                await unitOfWork.Repository<DiscountType>().AddAsync(d);
            await unitOfWork.SaveChangesAsync();
            var api = new DiscountTypesApiController(unitOfWork);

            Assert.IsType<BadRequestObjectResult>((await api.Create(new DiscountTypeDto { Name = "Too much", Value = 150m })).Result);

            var created = Assert.IsType<OkObjectResult>((await api.Create(new DiscountTypeDto
            {
                Name = "Suki card", ShortLabel = "SUKI", ValueKind = (int)DiscountValueKind.FixedAmount, Value = 50m
            })).Result);
            var suki = Assert.IsType<DiscountTypeDto>(created.Value);
            Assert.True(suki.IsActive);

            var all = Assert.IsType<OkObjectResult>(api.Get().Result);
            var list = ((ListResultDto<DiscountTypeDto>)all.Value!).Items;
            Assert.Equal(6, list.Count);
            Assert.Equal("Suki card", list[^1].Name); // statutory first, store discounts after

            var solo = list.Single(d => d.Category == (int)SpecialDiscountType.SoloParent);
            solo.IsActive = false;
            Assert.IsType<OkResult>(await api.Update(solo.Id, solo));

            var sync = new BirSyncController(unitOfWork, null!, Options.Create(new BirSupplierOptions()));
            sync.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };
            var pulled = Assert.IsType<OkObjectResult>((await sync.GetDiscountTypes()).Result);
            var names = ((List<DiscountTypeDto>)pulled.Value!).Select(d => d.Name).ToList();
            Assert.Equal(5, names.Count);
            Assert.DoesNotContain("Solo Parent", names);
            Assert.Contains("Suki card", names);
        }
    }
}
