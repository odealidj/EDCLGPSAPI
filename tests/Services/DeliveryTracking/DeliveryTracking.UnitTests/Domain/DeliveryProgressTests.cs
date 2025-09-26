namespace DeliveryTracking.UnitTests.Domain;

public class DeliveryProgressTests
{
    [Fact]
    public void Create_DenganParameterValid_HarusBikinAggregateDenganBenar()
    {
        // arrange
        var id = Guid.NewGuid();
        var deliveryNo = "DO-TOYOTA-001";
        var platNo = "B1234XYZ";
        var noKtp = "3171012345678901";
        var vendorName = "PT Telematika Global";
        var lpcd = "B1234XYZ";

        // act
        var progress = DeliveryProgress.Create(id, deliveryNo, platNo, noKtp, vendorName, lpcd);

        // assert
        progress.Should().NotBeNull();
        progress.Id.Should().Be(id);
        progress.DeliveryNo.Should().Be("DO-TOYOTA-001");
        progress.PlatNo.Should().Be("B1234XYZ");
        progress.NoKtp.Should().Be("3171012345678901");
        progress.VendorName.Should().Be(vendorName);
        progress.Lpcd.Should().Be("B1234XYZ");
    }

    [Fact]
    public void Create_DeliveryNoKosong_HarusLemparArgumentException()
    {
        // act
        var act = () => DeliveryProgress.Create(Guid.NewGuid(), "", "B1234XYZ", "3171012345");

        // assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_HarusUpdateFieldDenganBenar()
    {
        // arrange
        var progress = DeliveryProgress.Create(Guid.NewGuid(), "DO-001", "B1111AA", "12345");

        // act
        progress.UpdateDetails("B2222BB", "67890", "Vendor Baru", "LPCD99");

        // assert
        progress.PlatNo.Should().Be("B2222BB");
        progress.NoKtp.Should().Be("67890");
        progress.VendorName.Should().Be("Vendor Baru");
        progress.Lpcd.Should().Be("LPCD99");
    }
}
