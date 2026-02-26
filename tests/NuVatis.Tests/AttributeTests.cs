using NuVatis.Attributes;
using Xunit;

namespace NuVatis.Tests;

/**
 * Attribute 클래스들의 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-26
 */
public class AttributeTests {

    [Fact]
    public void SelectAttribute_Stores_Sql() {
        var attr = new SelectAttribute("SELECT * FROM users");
        Assert.Equal("SELECT * FROM users", attr.Sql);
    }

    [Fact]
    public void InsertAttribute_Stores_Sql() {
        var attr = new InsertAttribute("INSERT INTO users (name) VALUES (@name)");
        Assert.Equal("INSERT INTO users (name) VALUES (@name)", attr.Sql);
    }

    [Fact]
    public void DeleteAttribute_Stores_Sql() {
        var attr = new DeleteAttribute("DELETE FROM users WHERE id = @id");
        Assert.Equal("DELETE FROM users WHERE id = @id", attr.Sql);
    }

    [Fact]
    public void UpdateAttribute_Stores_Sql() {
        var attr = new UpdateAttribute("UPDATE users SET name = @name WHERE id = @id");
        Assert.Equal("UPDATE users SET name = @name WHERE id = @id", attr.Sql);
    }

    [Fact]
    public void ResultMapAttribute_Stores_Id() {
        var attr = new ResultMapAttribute("UserResultMap");
        Assert.Equal("UserResultMap", attr.ResultMapId);
    }
}
