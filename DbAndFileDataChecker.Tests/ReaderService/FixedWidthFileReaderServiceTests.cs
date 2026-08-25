using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace DbAndFileDataChecker.Tests
{
    [TestFixture]
    public class FixedWidthFileReaderServiceTests
    {
        [Test]
        public void ReadHeader_NullColumns_ReturnsEmptyArray()
        {
            // Arrange
            FixedWidthFileReaderService svc = new FixedWidthFileReaderService();

            // Act
            string[] result = svc.ReadHeader("some content", null);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ReadHeader_EmptyColumns_ReturnsEmptyArray()
        {
            // Arrange
            FixedWidthFileReaderService svc = new FixedWidthFileReaderService();

            // Act
            string[] result = svc.ReadHeader("content", new List<ColumnConfig>());

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ReadHeader_ColumnsWithNames_ReturnsNamesAndEmptyForNull()
        {
            // Arrange
            FixedWidthFileReaderService svc = new FixedWidthFileReaderService();
            var cols = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "First", StartColumn = 1, EndColumn = 5 },
                new ColumnConfig { Name = null, StartColumn = 6, EndColumn = 10 },
                new ColumnConfig { Name = "Third", StartColumn = 11, EndColumn = 15 }
            };

            // Act
            string[] result = svc.ReadHeader(string.Empty, cols);

            // Assert
            Assert.That(result.Length, Is.EqualTo(3));
            Assert.That(result[0], Is.EqualTo("First"));
            Assert.That(result[1], Is.EqualTo(string.Empty));
            Assert.That(result[2], Is.EqualTo("Third"));
        }

        [Test]
        public void ReadRows_NullOrEmptyColumns_YieldsNoRows()
        {
            // Arrange
            FixedWidthFileReaderService svc = new FixedWidthFileReaderService();
            string content = "Line1\nLine2\n";

            // Act
            var resultWithNull = svc.ReadRows(content, null).ToList();
            var resultWithEmpty = svc.ReadRows(content, new List<ColumnConfig>()).ToList();

            // Assert
            Assert.That(resultWithNull, Is.Empty);
            Assert.That(resultWithEmpty, Is.Empty);
        }

        [Test]
        public void ReadRows_SkipsBlankLines_AndTrimsValues_HandlesOutOfRangeAndNegativeLen()
        {
            // Arrange
            FixedWidthFileReaderService svc = new FixedWidthFileReaderService();

            // Columns:
            // ColA: chars 3-5
            // ColB: chars 8-10 (will be out of range for second line)
            // ColC: chars 14-16
            var cols = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "ColA", StartColumn = 3, EndColumn = 5 },
                new ColumnConfig { Name = "ColB", StartColumn = 8, EndColumn = 10 },
                new ColumnConfig { Name = "ColC", StartColumn = 14, EndColumn = 16 }
            };

            // Build content with one blank line, one line with extra spaces, and one short line
            string content = "\n" +
                             "  ABC  DEF   GHI  \n" + // has spaces, should be trimmed per column
                             "SHORT\n"; // second data line shorter than start index for ColB and ColC

            // Act
            var rows = svc.ReadRows(content, cols).ToList();

            // Assert
            // Should skip the initial blank line, so two data lines -> but one blank + two lines => 2 rows
            Assert.That(rows.Count, Is.EqualTo(2));

            // First data row assertions
            var first = rows[0];
            Assert.That(first.ContainsKey("ColA"));
            Assert.That(first.ContainsKey("ColB"));
            Assert.That(first.ContainsKey("ColC"));

            Assert.That(first["ColA"], Is.EqualTo("ABC")); // trimmed
            Assert.That(first["ColB"], Is.EqualTo("DEF"));
            // ColC has start 13 which exists on this longish line; check trimmed value
            Assert.That(first["ColC"], Is.EqualTo("GHI"));

            // Second data row ("SHORT") -> ColA should extract available chars (positions 3-5 -> "ORT"), ColB and ColC -> null
            var second = rows[1];
            Assert.That(second["ColA"], Is.EqualTo("ORT"));
            Assert.That(second["ColB"], Is.Null);
            Assert.That(second["ColC"], Is.Null);
        }

        [Test]
        public void ReadRows_StartIndexEqualsLineLength_SetsNull()
        {
            // Arrange
            FixedWidthFileReaderService svc = new FixedWidthFileReaderService();
            // Line length 4; start column 5 -> startIdx = 4 which equals line.Length => should set null
            var cols = new List<ColumnConfig>
            {
                new ColumnConfig { Name = "A", StartColumn = 5, EndColumn = 6 }
            };
            string content = "ABCD\n";

            // Act
            var rows = svc.ReadRows(content, cols).ToList();

            // Assert
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0]["A"], Is.Null);
        }
    }
}
