namespace PaycheckCalc.CloudSync;

public sealed class CloudSyncOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "";
    public string DatabaseId { get; set; } = "PaycheckCalc";
    public string ContainerId { get; set; } = "Paychecks";
}
