namespace PaladinHubV2.Common.Options;

public class PaymentOptions
{
    public const string SectionName = "Payments";

    public string OnlineProviderName { get; set; } = "DemoSportPay";

    public string OnlinePaymentLabel { get; set; } = "Card payment";

    public string[] SupportedMethods { get; set; } =
    [
        "online-card",
        "bank-transfer"
    ];

    public BankTransferOptions BankTransfer { get; set; } = new();
}

public class BankTransferOptions
{
    public string Beneficiary { get; set; } = "PaladinHubV2 Ltd.";

    public string Iban { get; set; } = "BG00DEMO12345678901234";

    public string Bic { get; set; } = "DEMOBGSF";

    public string BankName { get; set; } = "Demo Bank";

    public string ReferencePrefix { get; set; } = "SPORT";
}
