using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Reading.Internal;
using Xunit.Internal;

namespace FlameCsv.Tests.Reading;

public static class UnescapeTests
{
    public static readonly TheoryData<string, string> Entries =
    [
        .. Data.ReplaceLineEndings("\n")
            .Split('\n')
            .Select(static line =>
            {
                return new TheoryDataRow<string, string>(line, line.Replace("\"\"", "\""));
            }),
    ];

    [Theory]
    [MemberData(nameof(Entries))]
    public static void Should_Unescape_Field(string value, string expected)
    {
        uint quoteCount = (uint)value.Count('"');
        Assert.True(value.Length <= 128);
        Span<char> buffer = stackalloc char[128];

        Field.Unescape(
            quote: '"',
            destination: MemoryMarshal.Cast<char, ushort>(buffer),
            source: MemoryMarshal.Cast<char, ushort>(value.AsSpan()),
            quotesConsumed: quoteCount
        );

        Assert.Equal(expected, buffer.Slice(0, expected.Length));
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public static void Should_Unescape_Field_Utf8(string value, string expected)
    {
        uint quoteCount = (uint)value.Count('"');
        Assert.True(value.Length <= 128);
        Span<byte> buffer = stackalloc byte[128];

        Field.Unescape(
            quote: (byte)'"',
            destination: buffer,
            source: Encoding.UTF8.GetBytes(value),
            quotesConsumed: quoteCount
        );

        Assert.Equal(expected, Encoding.UTF8.GetString(buffer)[..expected.Length]);
    }

    private const string Data = """
1.7 Cubic Foot Compact ""Cube"" Office Refrigerators
Wilson Jones 1"" Hanging DublLock® Ring Binders
#10-4 1/8"" x 9 1/2"" Premium Diagonal Seam Envelopes
GBC Pre-Punched Binding Paper, Plastic, White, 8-1/2"" x 11""
Maxell 3.5"" DS/HD IBM-Formatted Diskettes, 10/Pack
Imation 3.5"" DS/HD IBM Formatted Diskettes, 10/Pack
IBM Multi-Purpose Copy Paper, 8 1/2 x 11"", Case
Adams Telephone Message Book W/Dividers/Space For Phone Numbers, 5 1/4""X8 1/2"", 300/Messages
Telephone Message Books with Fax/Mobile Section, 5 1/2"" x 3 3/16""
Staples Wirebound Steno Books, 6"" x 9"", 12/Pack
Linden® 12"" Wall Clock With Oak Frame
GBC Twin Loop™ Wire Binding Elements, 9/16"" Spine, Black
Tenex 46"" x 60"" Computer Anti-Static Chairmat, Rectangular Shaped
Wirebound Message Books, 2 7/8"" x 5"", 3 Forms per Page
Speediset Carbonless Redi-Letter® 7"" x 8 1/2""
Howard Miller 13-3/4"" Diameter Brushed Chrome Round Wall Clock
#10- 4 1/8"" x 9 1/2"" Security-Tint Envelopes
Executive Impressions 14"" Two-Color Numerals Wall Clock
Executive Impressions 13"" Chairman Wall Clock
Acme® 8"" Straight Scissors
Avery Trapezoid Extra Heavy Duty 4"" Binders
Rediform Wirebound ""Phone Memo"" Message Book, 11 x 5-3/4
Adams Write n' Stick Phone Message Book, 11"" X 5 1/4"", 200 Messages
Fiskars 8"" Scissors, 2/Pack
Wirebound Message Books, Four 2 3/4"" x 5"" Forms per Page, 600 Sets per Book
Acme Design Line 8"" Stainless Steel Bent Scissors w/Champagne Handles, 3-1/8"" Cut
Imation 3.5"", DISKETTE 44766 HGHLD3.52HD/FM, 10/Pack
Imation 3.5"" DS/HD IBM Formatted Diskettes, 50/Pack
Acme Hot Forged Carbon Steel Scissors with Nickel-Plated Handles, 3 7/8"" Cut, 8""L
Howard Miller 16"" Diameter Gallery Wall Clock
ACCOHIDE® 3-Ring Binder, Blue, 1""
Wilson Jones Hanging View Binder, White, 1""
Avery Trapezoid Ring Binder, 3"" Capacity, Black, 1040 sheets
Elite 5"" Scissors
Tenex Contemporary Contur Chairmats for Low and Medium Pile Carpet, Computer, 39"" x 49""
Barricks 18"" x 48"" Non-Folding Utility Table with Bottom Storage Shelf
Perma STOR-ALL™ Hanging File Box, 13 1/8""W x 12 1/4""D x 10 1/2""H
Bevis Round Bullnose 29"" High Table Top
Executive Impressions 13"" Clairmont Wall Clock
Manila Recycled Extra-Heavyweight Clasp Envelopes, 6"" x 9""
Acco Pressboard Covers with Storage Hooks, 14 7/8"" x 11"", Dark Blue
Rubbermaid ClusterMat Chairmats, Mat Size- 66"" x 60"", Lip 20"" x 11"" -90 Degree Angle
#10- 4 1/8"" x 9 1/2"" Recycled Envelopes
Staples #10 Laser & Inkjet Envelopes, 4 1/8"" x 9 1/2"", 100/Box
Wilson Jones Ledger-Size, Piano-Hinge Binder, 2"", Blue
Pressboard Covers with Storage Hooks, 9 1/2"" x 11"", Light Blue
Chromcraft Bull-Nose Wood 48"" x 96"" Rectangular Conference Tables
Executive Impressions 14"" Contract Wall Clock
Recycled Desk Saver Line ""While You Were Out"" Book, 5 1/2"" X 4""
Executive Impressions 14""
GE 48"" Fluorescent Tube, Cool White Energy Saver, 34 Watts, 30/Box
Seth Thomas 13 1/2"" Wall Clock
Imation Primaris 3.5"" 2HD Unformatted Diskettes, 10/Pack
Imation 3.5"" DS-HD Macintosh Formatted Diskettes, 10/Pack
Executive Impressions 8-1/2"" Career Panel/Partition Cubicle Clock
Executive Impressions 12"" Wall Clock
Tyvek Interoffice Envelopes, 9 1/2"" x 12 1/2"", 100/Box
Howard Miller 11-1/2"" Diameter Ridgewood Wall Clock
Seth Thomas 12"" Clock w/ Goldtone Case
It's Hot Message Books with Stickers, 2 3/4"" x 5""
Imation 3.5"" Diskettes, IBM Format, DS/HD, 10/Box, Neon
Career Cubicle Clock, 8 1/4"", Black
Executive Impressions 14"" Contract Wall Clock with Quartz Movement
Rush Hierlooms Collection 1"" Thick Stackable Bookcases
Eureka Recycled Copy Paper 8 1/2"" x 11"", Ream
Carina 42""Hx23 3/4""W Media Storage Unit
Executive Impressions 13-1/2"" Indoor/Outdoor Wall Clock
Imation 3.5"" Unformatted DS/HD Diskettes, 10/Box
Acco Four Pocket Poly Ring Binder with Label Holder, Smoke, 1""
1/4 Fold Party Design Invitations & White Envelopes, 24 8-1/2"" X 11"" Cards, 25 Env./Pack
Wirebound Service Call Books, 5 1/2"" x 4""
Howard Miller 13"" Diameter Goldtone Round Wall Clock
Acco Recycled 2"" Capacity Laser Printer Hanging Data Binders
Tensor ""Hersey Kiss"" Styled Floor Lamp
Wilson Jones Elliptical Ring 3 1/2"" Capacity Binders, 800 sheets
3.5"" IBM Formatted Diskettes, DS/HD
""While you Were Out"" Message Book, One Form per Page
REDIFORM Incoming/Outgoing Call Register, 11"" X 8 1/2"", 100 Messages
Tenex Traditional Chairmats for Medium Pile Carpet, Standard Lip, 36"" x 48""
Imation 3.5"", RTS 247544 3M 3.5 DSDD, 10/Pack
Mead 1st Gear 2"" Zipper Binder, Asst. Colors
Ampad® Evidence® Wirebond Steno Books, 6"" x 9""
Dana Fluorescent Magnifying Lamp, White, 36""
Avery® Durable Plastic 1"" Binders
Pressboard Data Binder, Crimson, 12"" X 8 1/2""
Black Print Carbonless 8 1/2"" x 8 1/4"" Rapid Memo Book
Acco Pressboard Covers with Storage Hooks, 14 7/8"" x 11"", Light Blue
Seth Thomas 14"" Putty-Colored Wall Clock
Telephone Message Books with Fax/Mobile Section, 4 1/4"" x 6""
Geographics Note Cards, Blank, White, 8 1/2"" x 11""
Eldon Delta Triangular Chair Mat, 52"" x 58"", Clear
Black Print Carbonless Snap-Off® Rapid Letter, 8 1/2"" x 7""
Iceberg OfficeWorks 42"" Round Tables
Tennsco Stur-D-Stor Boltless Shelving, 5 Shelves, 24"" Deep, Sand
IBM 3.5"" DS/HD IBM Formatted Diskettes, 50/Pack
Chromcraft 48"" x 96"" Racetrack Double Pedestal Table
Seth Thomas 8 1/2"" Cubicle Clock
Acco PRESSTEX® Data Binder with Storage Hooks, Dark Blue, 9 1/2"" X 11""
Computer Room Manger, 14""
Imation 3.5"" IBM-Formatted Diskettes, 10/Pack
Adams ""While You Were Out"" Message Pads
6"" Cubicle Wall Clock, Black
Staples 10"" Round Wall Clock
Adams Telephone Message Book W/Dividers/Space For Phone Numbers, 5 1/4""X8 1/2"", 200/Messages
Avery® 3 1/2"" Diskette Storage Pages, 10/Pack
""";
}
