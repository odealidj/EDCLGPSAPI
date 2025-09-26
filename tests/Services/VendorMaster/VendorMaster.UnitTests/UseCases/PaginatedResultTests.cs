using FluentAssertions;
using Shared.Pagination;
using Xunit;

namespace VendorMaster.UnitTests.UseCases;

public class PaginatedResultTests
{
    [Fact]
    public void PaginatedResult_DefaultConstructor_HarusMemilikiPageIndex1DanNextPrevFalse()
    {
        // Act
        var result = new PaginatedResult<string>();

        // Assert
        result.PageIndex.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Count.Should().Be(0);
        result.Next.Should().BeFalse();
        result.Prev.Should().BeFalse();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public void PaginatedResult_HalamanPertamaDenganDataLebih_HarusNextTrueDanPrevFalse()
    {
        // Arrange & Act
        var items = new[] { "item1", "item2" };
        var result = new PaginatedResult<string>(pageIndex: 1, pageSize: 2, count: 5, data: items);

        // Assert
        result.PageIndex.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.Count.Should().Be(5);
        result.Next.Should().BeTrue();
        result.Prev.Should().BeFalse();
    }

    [Fact]
    public void PaginatedResult_HalamanTengah_HarusNextTrueDanPrevTrue()
    {
        // Arrange & Act
        var items = new[] { "item3", "item4" };
        var result = new PaginatedResult<string>(pageIndex: 2, pageSize: 2, count: 5, data: items);

        // Assert
        result.PageIndex.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Count.Should().Be(5);
        result.Next.Should().BeTrue();
        result.Prev.Should().BeTrue();
    }

    [Fact]
    public void PaginatedResult_HalamanTerakhir_HarusNextFalseDanPrevTrue()
    {
        // Arrange & Act
        var items = new[] { "item5" };
        var result = new PaginatedResult<string>(pageIndex: 3, pageSize: 2, count: 5, data: items);

        // Assert
        result.PageIndex.Should().Be(3);
        result.PageSize.Should().Be(2);
        result.Count.Should().Be(5);
        result.Next.Should().BeFalse();
        result.Prev.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PaginatedResult_PageIndexNolAtauNegatif_HarusDinormalisasiMenjadi1(int rawPageIndex)
    {
        // Arrange & Act
        var items = new[] { "item1" };
        var result = new PaginatedResult<string>(pageIndex: rawPageIndex, pageSize: 10, count: 1, data: items);

        // Assert
        result.PageIndex.Should().Be(1);
        result.Prev.Should().BeFalse();
        result.Next.Should().BeFalse();
    }
}
