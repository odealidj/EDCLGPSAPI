namespace VendorMaster.UnitTests.Domain;

public class GpsVendorTests
{
    [Fact]
    public void Create_DenganParameterValid_HarusBikinAggregateDenganBenar()
    {
        // arrange & act
        var id = Guid.NewGuid();
        var vendor = GpsVendor.Create(
            id,
            "VND_TEST",
            "Vendor Test GPS",
            ProtocolType.REST_API.ToString(),
            VendorDefaults.DefaultTimezone,
            true,
            ProcessingStrategy.Individual.ToString());

        // assert
        vendor.Should().NotBeNull();
        vendor.Id.Should().Be(id);
        vendor.VendorCode.Should().Be("VND_TEST");
        vendor.VendorName.Should().Be("Vendor Test GPS");
        vendor.ProtocolType.Should().Be(ProtocolType.REST_API.ToString());
        vendor.Timezone.Should().Be(VendorDefaults.DefaultTimezone);
        vendor.IsActive.Should().BeTrue();
        vendor.ProcessingStrategy.Should().Be(ProcessingStrategy.Individual.ToString());
        vendor.GpsVendorEndpoints.Should().BeEmpty();
        vendor.Mappings.Should().BeEmpty();
        vendor.Lpcds.Should().BeEmpty();
        vendor.GpsVendorAuth.Should().BeNull();
    }

    [Fact]
    public void AddEndpoint_HarusNambahEndpointKeKoleksiChild()
    {
        // arrange
        var vendor = GpsVendor.Create(Guid.NewGuid(), "VND_TEST", "Vendor Test GPS");

        // act
        vendor.AddEndpoint(
            Guid.NewGuid(),
            EndpointType.TELEMETRY_LAST_POS.ToString(),
            "https://api.vendortest.com/gps",
            VendorDefaults.DefaultHttpMethod,
            VendorDefaults.DefaultContentType,
            null, null, null, null, null);

        // assert
        vendor.GpsVendorEndpoints.Should().HaveCount(1);
        var endpoint = vendor.GpsVendorEndpoints.First();
        endpoint.BaseUrl.Should().Be("https://api.vendortest.com/gps");
        endpoint.EndpointType.Should().Be(EndpointType.TELEMETRY_LAST_POS.ToString());
    }

    [Fact]
    public void SetAuth_HarusPasangKonfigurasiAuthVendor()
    {
        // arrange
        var vendor = GpsVendor.Create(Guid.NewGuid(), "VND_TEST", "Vendor Test GPS");

        // act
        vendor.SetAuth(
            Guid.NewGuid(),
            AuthType.BEARER_TOKEN.ToString(),
            "https://api.vendortest.com/auth",
            "POST",
            "application/json",
            "user1",
            "pass1",
            "token",
            "expires_in",
            3600,
            null, null, null);

        // assert
        vendor.GpsVendorAuth.Should().NotBeNull();
        vendor.GpsVendorAuth!.AuthType.Should().Be(AuthType.BEARER_TOKEN.ToString());
        vendor.GpsVendorAuth.Username.Should().Be("user1");
    }

    [Fact]
    public void AddMapping_HarusNambahFieldMapping()
    {
        // arrange
        var vendor = GpsVendor.Create(Guid.NewGuid(), "VND_TEST", "Vendor Test GPS");

        // act
        vendor.AddMapping(0, "lat", "Latitude", MappingDataType.NUMBER.ToString());

        // assert
        vendor.Mappings.Should().HaveCount(1);
        var mapping = vendor.Mappings.First();
        mapping.ResponseField.Should().Be("lat");
        mapping.MappedField.Should().Be("Latitude");
        mapping.DataType.Should().Be(MappingDataType.NUMBER.ToString());
    }

    [Fact]
    public void AddLpcd_HarusNambahUnitArmadaToyota()
    {
        // arrange
        var vendor = GpsVendor.Create(Guid.NewGuid(), "VND_TEST", "Vendor Test GPS");

        // act
        vendor.AddLpcd(Guid.NewGuid(), "B1234XYZ");

        // assert
        vendor.Lpcds.Should().HaveCount(1);
        var lpcd = vendor.Lpcds.First();
        lpcd.Lpcd.Should().Be("B1234XYZ");
    }

    [Fact]
    public void UpdateDetails_HarusUpdateFieldIndukDenganBenar()
    {
        // arrange
        var vendor = GpsVendor.Create(Guid.NewGuid(), "VND_OLD", "Old Name");
        var initialVersion = vendor.Version;

        // act
        vendor.UpdateDetails(
            "VND_NEW",
            "New Name",
            ProtocolType.MQTT.ToString(),
            "UTC",
            false,
            ProcessingStrategy.Combined.ToString(),
            null,
            null);

        // assert
        vendor.VendorCode.Should().Be("VND_NEW");
        vendor.VendorName.Should().Be("New Name");
        vendor.ProtocolType.Should().Be(ProtocolType.MQTT.ToString());
        vendor.Timezone.Should().Be("UTC");
        vendor.IsActive.Should().BeFalse();
        vendor.ProcessingStrategy.Should().Be(ProcessingStrategy.Combined.ToString());
        vendor.Version.Should().Be(initialVersion + 1);
    }
}
