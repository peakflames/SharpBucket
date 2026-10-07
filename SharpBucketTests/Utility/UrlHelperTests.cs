using System;
using NUnit.Framework;
using SharpBucket.Utility;
using Shouldly;

namespace SharpBucketTests.Utility
{
    [TestFixture]
    public class UrlHelperTests
    {
        [TestCase("src/main/dir/file.txt")]
        [TestCase("feature/x")]
        [TestCase("/leading/slash")]
        [TestCase("a..b/file.txt")]
        public void ConcatPathSegments_ValidSegment_ShouldBeAccepted(string segment)
        {
            Should.NotThrow(() => UrlHelper.ConcatPathSegments("src", segment));
        }

        [Test]
        public void ConcatPathSegments_ShouldJoinSegmentsAndCollapseDoubleSlashes()
        {
            UrlHelper.ConcatPathSegments("src", "feature/x", "/dir", "file.txt").ShouldBe("src/feature/x/dir/file.txt");
        }

        [Test]
        public void ConcatPathSegments_ShouldStopAtTheFirstNullOrEmptySegment()
        {
            UrlHelper.ConcatPathSegments("src", "main", null, "ignored").ShouldBe("src/main");
            UrlHelper.ConcatPathSegments("src", "", "ignored").ShouldBe("src");
        }

        [TestCase("..")]
        [TestCase("a/../b")]
        [TestCase("./a")]
        [TestCase("a/.")]
        [TestCase("%2e%2e/x")]
        [TestCase("..%2fx")]
        [TestCase("a?b")]
        [TestCase("a#b")]
        [TestCase("a\\b")]
        [TestCase("a%3fb")]
        [TestCase("a%23b")]
        [TestCase("a%5Cb")]
        [TestCase("a\nb")]
        [TestCase("a%0Ab")]
        public void ConcatPathSegments_InvalidSegment_ShouldThrow(string segment)
        {
            Should.Throw<ArgumentException>(() => UrlHelper.ConcatPathSegments("src", "main", segment));
            Should.Throw<ArgumentException>(() => UrlHelper.ConcatPathSegments(segment, "main"));
        }
    }
}
