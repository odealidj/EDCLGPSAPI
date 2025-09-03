using Microsoft.Extensions.Configuration;

namespace GeofenceWorker.Services.RabbitMq;

public class RabbitMqSettings
{
    ////[ConfigurationKeyName("Protocol")]
    public string Protocol { get; set; } //= "amqps";
    ////[ConfigurationKeyName("HostName")]
    public string HostName { get; set; } //= "b-dae6f57a-61c3-40a4-8854-8e5e382cca6d.mq.ap-southeast-2.amazonaws.com";
    public string UserName { get; set; } //= "admin-mq";
    public string Password { get; set; } //= "06JMn0pXJTOOuVV";
    public int Port { get; set; } //= 5671;
    public string VirtualHost { get; set; } //= "";

    public RabbitMqSettings()
    {
        
    }

    public RabbitMqSettings(string protocol, string hostName, string userName, string password, int port, string virtualHost)
    {
        Protocol = protocol;
        HostName = hostName;
        UserName = userName;
        Password = password;
        Port = port;
        VirtualHost = virtualHost;
        
    }
}

