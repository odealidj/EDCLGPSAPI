namespace VendorMaster.UnitTests.Validation;

public class CreateVendorValidatorTests
{
    private readonly CreateVendorValidator _validator = new();

    [Fact]
    public void Validate_PayloadValid_HarusLolosValidasi()
    {
        // arrange
        var command = new CreateVendorCommand(new VendorDto
        {
            VendorCode = "VND_TOYOTA",
            VendorName = "Toyota Logistics GPS",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString(),
            Lpcd = "TOYOTA_LPCD"
        });

        // act
        var result = _validator.Validate(command);

        // assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_LpcdKosong_HarusGagalValidasi()
    {
        // arrange
        var command = new CreateVendorCommand(new VendorDto
        {
            VendorCode = "VND_TOYOTA",
            VendorName = "Toyota Logistics GPS",
            ProtocolType = ProtocolType.REST_API.ToString(),
            ProcessingStrategy = ProcessingStrategy.Individual.ToString()
        });

        // act
        var result = _validator.Validate(command);

        // assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.Contains("Lpcds"));
    }

    [Theory]
    [InlineData("", "Nama Vendor")]
    [InlineData("VND_01", "")]
    public void Validate_VendorCodeAtauNamaKosong_HarusGagalValidasi(string code, string name)
    {
        // arrange
        var command = new CreateVendorCommand(new VendorDto
        {
            VendorCode = code,
            VendorName = name
        });

        // act
        var result = _validator.Validate(command);

        // assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_ProtocolTypeNgawur_HarusGagalValidasi()
    {
        // arrange
        var command = new CreateVendorCommand(new VendorDto
        {
            VendorCode = "VND_01",
            VendorName = "Vendor Valid",
            ProtocolType = "TIDAK_DIKENAL"
        });

        // act
        var result = _validator.Validate(command);

        // assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.Contains("ProtocolType"));
    }

    [Fact]
    public void Validate_AuthTypeNgawur_HarusGagalValidasi()
    {
        // arrange
        var command = new CreateVendorCommand(new VendorDto
        {
            VendorCode = "VND_01",
            VendorName = "Vendor Valid",
            Auth = new VendorAuthDto
            {
                Authtype = "INVALID_AUTH"
            }
        });

        // act
        var result = _validator.Validate(command);

        // assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.Contains("Authtype"));
    }
}
