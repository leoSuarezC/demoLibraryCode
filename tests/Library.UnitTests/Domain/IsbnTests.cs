using Library.Domain;

namespace Library.UnitTests.Domain;

public class IsbnTests
{
    [Theory]
    [InlineData("978-0-441-01359-3", "9780441013593")]
    [InlineData("9780441013593", "9780441013593")]
    [InlineData("0 441 01359 8", "0441013598")]
    [InlineData("080442957x", "080442957X")]
    public void Separators_are_stripped_and_the_check_letter_upper_cased(string raw, string expected) =>
        Assert.Equal(expected, Isbn13.Normalise(raw));

    [Theory]
    [InlineData("9780441013593")]   // Dune
    [InlineData("9780132350884")]   // Clean Code
    public void Valid_isbn13_is_accepted(string isbn) =>
        Assert.True(Isbn13.IsValid(isbn));

    [Fact]
    public void Transposed_digits_are_rejected_by_the_check_digit()
    {
        // 9780441013593 with two digits swapped - still 13 digits, no longer valid.
        Assert.False(Isbn13.IsValid("9780441015393"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("97804410135931")]
    public void Anything_that_is_not_10_or_13_characters_is_rejected(string isbn) =>
        Assert.False(Isbn13.IsValid(isbn));

    [Fact]
    public void Isbn10_ending_in_x_is_accepted() =>
        Assert.True(Isbn13.IsValid("080442957X"));
}
