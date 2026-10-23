using AwesomeAssertions;
using Nop.Core.Domain.Messages;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.Messages;

[TestFixture]
public class TokenizerTests : ServiceTest
{
    private ITokenizer _tokenizer;
    private MessageTemplatesSettings _messageTemplatesSettings;
    private bool _defaultCaseInvariantReplacement;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _messageTemplatesSettings = GetService<MessageTemplatesSettings>();
        _defaultCaseInvariantReplacement = _messageTemplatesSettings.CaseInvariantReplacement;
        _messageTemplatesSettings.CaseInvariantReplacement = false;
        await GetService<ISettingService>().SaveSettingAsync(_messageTemplatesSettings);

        _tokenizer = GetService<ITokenizer>();
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        _messageTemplatesSettings.CaseInvariantReplacement = _defaultCaseInvariantReplacement;
        await GetService<ISettingService>().SaveSettingAsync(_messageTemplatesSettings);
    }

    [Test]
    public void CanReplaceTokensCaseSensitive()
    {
        var tokens = new List<Token>
        {
            new("Token1", "Value1")
        };
        //correct case
        _tokenizer
            .Replace("Some text %Token1%", tokens, false)
            .Should().Be("Some text Value1");
        //wrong case
        _tokenizer
            .Replace("Some text %TOKeN1%", tokens, false)
            .Should().Be("Some text %TOKeN1%");
    }

    [Test]
    public async Task CanReplaceTokensCaseInvariant()
    {
        _messageTemplatesSettings.CaseInvariantReplacement = true;
        await GetService<ISettingService>().SaveSettingAsync(_messageTemplatesSettings);

        var tokenizer = GetService<ITokenizer>();

        var tokens = new List<Token>
        {
            new("Token1", "Value1")
        };
        tokenizer
            .Replace("Some text %TOKEn1%", tokens, false)
            .Should().Be("Some text Value1");
    }

    [Test]
    public void CanHtmlEncode()
    {
        var tokens = new List<Token>
        {
            new("Token1", "<Value1>")
        };

        _tokenizer
            .Replace("Some text %Token1%", tokens, true)
            .Should().Be("Some text &lt;Value1&gt;");
    }

    [Test]
    public void ShouldNotHtmlEncodeIfTokenDoesNotAllowIt()
    {
        var tokens = new List<Token>
        {
            new("Token1", "<Value1>", true)
        };

        _tokenizer
            .Replace("Some text %Token1%", tokens, true)
            .Should().Be("Some text <Value1>");
    }

    [Test]
    public void CanReplaceTokensWithConditionalStatements()
    {
        var tokens = new List<Token>
        {
            new("ConditionToken", true),
            new("ConditionToken2", 2),
            new("ThenToken", "value"),
            new("ThenToken2", "value2"),
            new("SomeValueToken", 10),
        };

        //simple condition
        _tokenizer.Replace(@"Some text %if (%ConditionToken%) %ThenToken% endif% %SomeValueToken%", tokens, true)
            .Should().Be("Some text value  10");

        //broken token in condition
        _tokenizer.Replace(@"Some text %if (ConditionToken%) %ThenToken% endif% %SomeValueToken%", tokens, true)
            .Should().Be("Some text  10");

        //multiple conditions
        _tokenizer.Replace(@"Some text %if (%ConditionToken% && %ConditionToken2% > 1) %ThenToken% endif% %SomeValueToken%", tokens, true)
            .Should().Be("Some text value  10");

        //nested conditional statements
        _tokenizer.Replace(@"Some text %if (%ConditionToken%) %ThenToken% %if (%ConditionToken2% > 1) %ThenToken2% endif% %if (!%ConditionToken%) %ThenToken% endif% %ThenToken% endif% %SomeValueToken%", tokens, true)
            .Should().Be("Some text value value2   value  10");

        //wrong condition
        _tokenizer.Replace(@"Some text %if (%ConditionToken%) %ThenToken%", tokens, true)
            .Should().Be("Some text %if (True) value");

        //complex condition
        _tokenizer.Replace(@"Some text %if (%ConditionToken%) %ThenToken% endif% %SomeValueToken% %if (%ConditionToken2% > 1) %ThenToken2% %if (!%ConditionToken%) %ThenToken% endif% endif% %SomeValueToken%", tokens, true)
            .Should().Be("Some text value  10 value2   10");
    }

    [Test]
    public void CanReplaceTokensWithNonStringValues()
    {
        var tokens = new List<Token>
        {
            new("Token1", true),
            new("Token2", 1),
            new("Token3", "value")
        };

        _tokenizer.Replace("Some text %Token1%, %Token2%, %Token3%", tokens, true)
            .Should().Be("Some text True, 1, value");
    }

    [Test]
    public void CanReplaceTokensWithTrackingConditions()
    {
        var template = @"%if (!String.IsNullOrEmpty(%Shipment.TrackingNumber%))Tracking number: %if (!String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))<a href=""%Shipment.TrackingNumberURL%"" target=""_blank"">%Shipment.TrackingNumber%</a>endif%%if (String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))%Shipment.TrackingNumber%endif%<br />endif%";

        // Case 1: Tracking number and URL both present
        var tokensWithBoth = new List<Token>
        {
            new("Shipment.TrackingNumber", "1Z9999999"),
            new("Shipment.TrackingNumberURL", "https://ups.com/track?num=1Z9999999", true)
        };
        var res1 = _tokenizer.Replace(template, tokensWithBoth, false);
        res1.Should().Be(@"Tracking number: <a href=""https://ups.com/track?num=1Z9999999"" target=""_blank"">1Z9999999</a><br />");

        // Case 2: Tracking number present, URL empty
        var tokensWithNumOnly = new List<Token>
        {
            new("Shipment.TrackingNumber", "1Z9999999"),
            new("Shipment.TrackingNumberURL", "", true)
        };
        var res2 = _tokenizer.Replace(template, tokensWithNumOnly, false);
        res2.Should().Be(@"Tracking number: 1Z9999999<br />");

        // Case 3: Both empty
        var tokensEmpty = new List<Token>
        {
            new("Shipment.TrackingNumber", ""),
            new("Shipment.TrackingNumberURL", "", true)
        };
        var res3 = _tokenizer.Replace(template, tokensEmpty, false);
        res3.Should().Be(string.Empty);

        // Case 4: Null tracking number
        var tokensNull = new List<Token>
        {
            new("Shipment.TrackingNumber", null),
            new("Shipment.TrackingNumberURL", "", true)
        };
        var res4 = _tokenizer.Replace(template, tokensNull, false);
        res4.Should().Be(string.Empty);

        // Case 5: Missing tokens
        var tokensMissing = new List<Token>();
        var res5 = _tokenizer.Replace(template, tokensMissing, false);
        res5.Should().Be(string.Empty);
    }
}