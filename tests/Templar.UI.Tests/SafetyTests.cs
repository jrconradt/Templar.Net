using Templar.UI;
using Xunit;

namespace Templar.UI.Tests;

public class SafetyTests
{
    [Fact]
    public void Vbscript_url_is_blocked()
    {
        Assert.Equal(Safety.BlockedUrl, Safety.SanitizeUrl("vbscript:msgbox(1)"));
    }

    [Fact]
    public void Vbscript_url_through_attribute_is_blocked()
    {
        var attr = new Attr { Name = "href", Value = "vbscript:msgbox(1)" };
        Assert.Equal(" href=\"about:invalid#blocked\"", attr.Render());
    }

    [Theory]
    [InlineData("href")]
    [InlineData("src")]
    [InlineData("srcset")]
    [InlineData("action")]
    [InlineData("formaction")]
    [InlineData("cite")]
    [InlineData("poster")]
    [InlineData("data")]
    [InlineData("background")]
    [InlineData("ping")]
    [InlineData("longdesc")]
    [InlineData("manifest")]
    [InlineData("xlink:href")]
    public void Url_attribute_routes_through_sanitization(string name)
    {
        Assert.True(Safety.IsUrlAttribute(name));
        var attr = new Attr { Name = name, Value = "javascript:alert(1)" };
        Assert.Equal($" {name}=\"about:invalid#blocked\"", attr.Render());
    }

    [Fact]
    public void Url_attribute_is_case_insensitive()
    {
        Assert.True(Safety.IsUrlAttribute("HREF"));
        Assert.True(Safety.IsUrlAttribute("Src"));
    }

    [Fact]
    public void Non_url_attribute_is_not_routed()
    {
        Assert.False(Safety.IsUrlAttribute("title"));
        Assert.False(Safety.IsUrlAttribute("id"));
    }

    [Fact]
    public void Leading_control_chars_in_scheme_are_stripped()
    {
        Assert.Equal(Safety.BlockedUrl, Safety.SanitizeUrl("\x01\x02javascript:alert(1)"));
    }

    [Fact]
    public void Embedded_whitespace_in_scheme_is_stripped()
    {
        Assert.Equal(Safety.BlockedUrl, Safety.SanitizeUrl("java script:alert(1)"));
    }

    [Fact]
    public void Embedded_newline_in_scheme_is_stripped()
    {
        Assert.Equal(Safety.BlockedUrl, Safety.SanitizeUrl("vb\nscript:alert(1)"));
    }

    [Fact]
    public void Trailing_whitespace_before_colon_is_stripped()
    {
        Assert.Equal(Safety.BlockedUrl, Safety.SanitizeUrl("javascript :alert(1)"));
    }

    [Fact]
    public void Path_before_colon_is_not_a_scheme()
    {
        Assert.Equal("/path:notscheme", Safety.SanitizeUrl("/path:notscheme"));
    }

    [Fact]
    public void Query_before_colon_is_not_a_scheme()
    {
        Assert.Equal("a?b:c", Safety.SanitizeUrl("a?b:c"));
    }

    [Fact]
    public void Fragment_before_colon_is_not_a_scheme()
    {
        Assert.Equal("a#b:c", Safety.SanitizeUrl("a#b:c"));
    }

    [Fact]
    public void Relative_url_with_no_colon_is_preserved()
    {
        Assert.Equal("/images/a.png", Safety.SanitizeUrl("/images/a.png"));
    }

    [Theory]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("data:image/jpeg;base64,/9j/4AAQ=")]
    [InlineData("data:image/gif;base64,R0lGODlh")]
    [InlineData("data:image/webp;base64,UklGRg==")]
    public void Safe_image_data_url_is_preserved(string url)
    {
        Assert.Equal(url, Safety.SanitizeUrl(url));
    }

    [Fact]
    public void Data_image_url_without_parameters_is_preserved()
    {
        Assert.Equal("data:image/png,abc", Safety.SanitizeUrl("data:image/png,abc"));
    }

    [Fact]
    public void Data_image_media_type_match_is_case_insensitive()
    {
        Assert.Equal("data:IMAGE/PNG;base64,AAAA", Safety.SanitizeUrl("data:IMAGE/PNG;base64,AAAA"));
    }

    [Theory]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("data:image/svg+xml,<svg onload=alert(1)>")]
    [InlineData("data:application/javascript,alert(1)")]
    [InlineData("data:,plain")]
    public void Unsafe_data_url_is_blocked(string url)
    {
        Assert.Equal(Safety.BlockedUrl, Safety.SanitizeUrl(url));
    }

    [Fact]
    public void Data_url_without_comma_is_blocked()
    {
        Assert.Equal(Safety.BlockedUrl, Safety.SanitizeUrl("data:image/png"));
    }

    [Fact]
    public void Srcset_single_safe_candidate_is_preserved()
    {
        Assert.Equal("/a.png 1x", Safety.SanitizeSrcset("/a.png 1x"));
    }

    [Fact]
    public void Srcset_multiple_safe_candidates_are_preserved()
    {
        Assert.Equal("/a.png 1x, /b.png 2x", Safety.SanitizeSrcset("/a.png 1x, /b.png 2x"));
    }

    [Fact]
    public void Srcset_blocks_dangerous_candidate_only()
    {
        Assert.Equal(
            "/safe.png 1x, about:invalid#blocked 2x",
            Safety.SanitizeSrcset("/safe.png 1x, javascript:alert(1) 2x"));
    }

    [Fact]
    public void Srcset_blocks_first_candidate_when_dangerous()
    {
        Assert.Equal(
            "about:invalid#blocked 1x, /ok.png 2x",
            Safety.SanitizeSrcset("javascript:alert(1) 1x, /ok.png 2x"));
    }

    [Fact]
    public void Srcset_candidate_without_descriptor_is_sanitized()
    {
        Assert.Equal("about:invalid#blocked", Safety.SanitizeSrcset("vbscript:msgbox(1)"));
    }

    [Fact]
    public void Srcset_preserves_surrounding_whitespace_layout()
    {
        Assert.Equal("  /a.png 1x , /b.png 2x", Safety.SanitizeSrcset("  /a.png 1x , /b.png 2x"));
    }

    [Fact]
    public void Srcset_name_aware_entry_routes_to_grammar()
    {
        Assert.Equal(
            "/safe.png 1x, about:invalid#blocked 2x",
            Safety.SanitizeUrl("srcset", "/safe.png 1x, javascript:alert(1) 2x"));
    }

    [Fact]
    public void Name_aware_entry_routes_non_srcset_to_single_url()
    {
        Assert.Equal(Safety.BlockedUrl, Safety.SanitizeUrl("href", "javascript:alert(1)"));
    }
}
