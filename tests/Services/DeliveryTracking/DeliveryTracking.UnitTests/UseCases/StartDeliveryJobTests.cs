using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.UseCases.StartDeliveryJob;
using FluentAssertions;
using Xunit;

namespace DeliveryTracking.UnitTests.UseCases;

public class StartDeliveryJobTests
{
    private readonly StartDeliveryJobCommandValidator _validator = new();

    [Fact]
    public void Validator_PayloadValid_HarusLolosValidasi()
    {
        // arrange
        var command = new StartDeliveryJobCommand(new StartDeliveryJobRequestDto
        {
            DeliveryNo = "DO-12345",
            Lpcd = "AJL",
            Key = "PlatNo",
            Val = "B 1234 XYZ"
        });

        // act
        var result = _validator.Validate(command);

        // assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "AJL", "PlatNo", "B 1234 XYZ")]
    [InlineData("DO-12345", "", "PlatNo", "B 1234 XYZ")]
    [InlineData("DO-12345", "AJL", "PlatNo", "")]
    [InlineData("DO-12345", "AJL", "InvalidMatchingKey", "B 1234 XYZ")]
    public void Validator_PayloadTidakValid_HarusGagalValidasi(
        string deliveryNo, string lpcd, string key, string val)
    {
        // arrange
        var command = new StartDeliveryJobCommand(new StartDeliveryJobRequestDto
        {
            DeliveryNo = deliveryNo,
            Lpcd = lpcd,
            Key = key,
            Val = val
        });

        // act
        var result = _validator.Validate(command);

        // assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("B 9037 PXS", "B9037PXS")]
    [InlineData("b-1234_xyz", "B1234XYZ")]
    [InlineData("  D 9999 AA  ", "D9999AA")]
    [InlineData("", "")]
    public void DeliveryValidator_CleanKey_HarusMembersihkanKarakterNonAlfanumerik(string input, string expected)
    {
        var clean = DeliveryValidator.CleanKey(input);
        clean.Should().Be(expected);
    }

    [Theory]
    [InlineData("PlatNo", true)]
    [InlineData("platno", true)]
    [InlineData("VehicleId", true)]
    [InlineData("Imei", true)]
    [InlineData("DriverKtp", true)]
    [InlineData("DeliveryNo", true)]
    [InlineData("RandomKey", false)]
    public void DeliveryValidator_IsValidKey_HarusValidasiWhitelistKey(string key, bool expected)
    {
        var isValid = DeliveryValidator.IsValidKey(key);
        isValid.Should().Be(expected);
    }
}
