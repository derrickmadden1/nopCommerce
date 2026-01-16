using System.Data;
using FluentMigrator;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Tax;
using Nop.Data.Extensions;
using Nop.Data.Mapping;

namespace Nop.Data.Migrations.UpgradeTo460;

[NopSchemaMigration("2022-07-20 00:00:10", "SchemaMigration for 4.60.0")]
public class SchemaMigration : ForwardOnlyMigration
{
    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        //add column
        this.AddOrAlterColumnFor<Customer>(t => t.FirstName)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.LastName)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.Gender)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.DateOfBirth)
            .AsDateTime2()
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.Company)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.StreetAddress)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.StreetAddress2)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.ZipPostalCode)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.City)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.County)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.CountryId)
            .AsInt32()
            .NotNullable()
            .SetExistingRowsTo(0);

        this.AddOrAlterColumnFor<Customer>(t => t.StateProvinceId)
            .AsInt32()
            .NotNullable()
            .SetExistingRowsTo(0);

        this.AddOrAlterColumnFor<Customer>(t => t.Phone)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.Fax)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.VatNumber)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.VatNumberStatusId)
            .AsInt32()
            .NotNullable()
            .SetExistingRowsTo((int)VatNumberStatus.Unknown);

        this.AddOrAlterColumnFor<Customer>(t => t.TimeZoneId)
            .AsString(1000)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.CustomCustomerAttributesXML)
            .AsString(int.MaxValue)
            .Nullable();

        this.AddOrAlterColumnFor<Customer>(t => t.CurrencyId)
            .AsInt32()
            .Nullable();

        var customerTableName = NameCompatibilityManager.GetTableName(typeof(Customer));
        var currencyIdColumnName = NameCompatibilityManager.GetColumnName(typeof(Customer), nameof(Customer.CurrencyId));
        var currencyTableName = NameCompatibilityManager.GetTableName(typeof(Currency));

        if (!Schema.Table(customerTableName).Index("IX_Customer_CurrencyId").Exists())
        {
            Create.Index("IX_Customer_CurrencyId").OnTable(customerTableName).OnColumn(currencyIdColumnName).Ascending();
        }

        //add foreign key
        if (!Schema.Table(customerTableName).Constraint($"FK_{customerTableName}_{currencyIdColumnName}_{currencyTableName}_Id").Exists())
        {
            Create.ForeignKey($"FK_{customerTableName}_{currencyIdColumnName}_{currencyTableName}_Id")
                .FromTable(customerTableName).ForeignColumn(currencyIdColumnName)
                .ToTable(currencyTableName).PrimaryColumn(nameof(Currency.Id))
                .OnDelete(Rule.SetNull);
        }

        this.AddOrAlterColumnFor<Customer>(t => t.LanguageId)
            .AsInt32()
            .Nullable();

        var languageIdColumnName = NameCompatibilityManager.GetColumnName(typeof(Customer), nameof(Customer.LanguageId));
        var languageTableName = NameCompatibilityManager.GetTableName(typeof(Language));

        if (!Schema.Table(customerTableName).Index("IX_Customer_LanguageId").Exists())
        {
            Create.Index("IX_Customer_LanguageId").OnTable(customerTableName).OnColumn(languageIdColumnName).Ascending();
        }

        //add foreign key
        if (!Schema.Table(customerTableName).Constraint($"FK_{customerTableName}_{languageIdColumnName}_{languageTableName}_Id").Exists())
        {
            Create.ForeignKey($"FK_{customerTableName}_{languageIdColumnName}_{languageTableName}_Id")
                .FromTable(customerTableName).ForeignColumn(languageIdColumnName)
                .ToTable(languageTableName).PrimaryColumn(nameof(Language.Id))
                .OnDelete(Rule.SetNull);
        }

        this.AddOrAlterColumnFor<Customer>(t => t.TaxDisplayTypeId)
            .AsInt32()
            .Nullable();

        // 5705
        this.AddOrAlterColumnFor<Discount>(t => t.IsActive)
            .AsBoolean()
            .NotNullable()
            .SetExistingRowsTo(true);
    }
}