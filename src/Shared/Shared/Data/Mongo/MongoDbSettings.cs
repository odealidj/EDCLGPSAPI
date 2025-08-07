namespace Shared.Data.Mongo;

public class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "mongodb://admin:adminpassword@localhost:27017/?authSource=admin";
    public string DatabaseName { get; set; } = "edcl_logs_db";
    public string ApiLogsCollection { get; set; } = "gps_api_logs";
    public string RawResponsesCollection { get; set; } = "gps_raw_responses";
    public int ApiLogTtlDays { get; set; } = 90;
    public int RawResponseTtlDays { get; set; } = 30;
}
