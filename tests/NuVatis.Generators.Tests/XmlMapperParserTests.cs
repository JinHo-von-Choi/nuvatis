using System.Linq;
using System.Threading;
using NuVatis.Generators.Models;
using NuVatis.Generators.Parsing;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * XmlMapperParser 단위 테스트.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public class XmlMapperParserTests {

    [Fact]
    public void ParseSimpleSelect() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <select id=""GetUser"">
        SELECT * FROM users
    </select>
</mapper>";

        var mapper = XmlMapperParser.Parse(xml, CancellationToken.None);

        Assert.Equal("UserMapper", mapper.Namespace);
        Assert.Single(mapper.Statements);
        Assert.Equal("GetUser", mapper.Statements[0].Id);
        Assert.Equal("Select", mapper.Statements[0].StatementType);
        Assert.IsType<TextNode>(mapper.Statements[0].RootNode);
        Assert.Contains("SELECT * FROM users", ((TextNode)mapper.Statements[0].RootNode).Text);
    }

    [Fact]
    public void ParseResultMap() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <resultMap id=""UserResult"" type=""User"">
        <id column=""user_id"" property=""UserId"" />
        <result column=""user_name"" property=""Name"" />
        <result column=""email"" property=""Email"" />
    </resultMap>
</mapper>";

        var mapper = XmlMapperParser.Parse(xml, CancellationToken.None);

        Assert.Single(mapper.ResultMaps);
        var rm = mapper.ResultMaps[0];
        Assert.Equal("UserResult", rm.Id);
        Assert.Equal("User", rm.Type);
        Assert.Equal(3, rm.Mappings.Length);
        Assert.True(rm.Mappings[0].IsId);
        Assert.Equal("user_id", rm.Mappings[0].Column);
        Assert.Equal("UserId", rm.Mappings[0].Property);
        Assert.False(rm.Mappings[1].IsId);
        Assert.Equal("user_name", rm.Mappings[1].Column);
    }

    [Fact]
    public void ParseDynamicSqlIf() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <select id=""Search"">
        SELECT * FROM users
        <if test=""name != null"">
            WHERE name = #{name}
        </if>
    </select>
</mapper>";

        var mapper    = XmlMapperParser.Parse(xml, CancellationToken.None);
        var statement = mapper.Statements[0];

        var root = Assert.IsType<MixedNode>(statement.RootNode);
        var ifNode = root.Children.OfType<IfNode>().First();
        Assert.Equal("name != null", ifNode.Test);
        Assert.True(ifNode.Children.Length > 0);
    }

    [Fact]
    public void ParseDynamicSqlWhere() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <select id=""Search"">
        SELECT * FROM users
        <where>
            <if test=""id != null"">AND id = #{id}</if>
        </where>
    </select>
</mapper>";

        var mapper    = XmlMapperParser.Parse(xml, CancellationToken.None);
        var statement = mapper.Statements[0];

        var root      = Assert.IsType<MixedNode>(statement.RootNode);
        var whereNode = root.Children.OfType<WhereNode>().First();
        var ifNode    = whereNode.Children.OfType<IfNode>().First();
        Assert.Equal("id != null", ifNode.Test);
    }

    [Fact]
    public void ParseForEach() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <select id=""ListByIds"">
        SELECT * FROM users WHERE id IN
        <foreach collection=""ids"" item=""id"" open=""("" close="")"" separator="","">
            #{id}
        </foreach>
    </select>
</mapper>";

        var mapper    = XmlMapperParser.Parse(xml, CancellationToken.None);
        var statement = mapper.Statements[0];

        var root        = Assert.IsType<MixedNode>(statement.RootNode);
        var forEachNode = root.Children.OfType<ForEachNode>().First();
        Assert.Equal("ids", forEachNode.Collection);
        Assert.Equal("id", forEachNode.Item);
        Assert.Equal("(", forEachNode.Open);
        Assert.Equal(")", forEachNode.Close);
        Assert.Equal(",", forEachNode.Separator);
    }

    [Fact]
    public void ParseChooseWhenOtherwise() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <select id=""Order"">
        SELECT * FROM users
        <choose>
            <when test=""sortBy == 'name'"">ORDER BY name</when>
            <otherwise>ORDER BY id</otherwise>
        </choose>
    </select>
</mapper>";

        var mapper    = XmlMapperParser.Parse(xml, CancellationToken.None);
        var statement = mapper.Statements[0];

        var root       = Assert.IsType<MixedNode>(statement.RootNode);
        var chooseNode = root.Children.OfType<ChooseNode>().First();
        Assert.Single(chooseNode.Whens);
        Assert.Equal("sortBy == 'name'", chooseNode.Whens[0].Test);
        Assert.NotNull(chooseNode.Otherwise);
    }

    [Fact]
    public void ParseParameterBinding() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <select id=""Get"">
        SELECT * FROM users WHERE id = #{id}
    </select>
</mapper>";

        var mapper    = XmlMapperParser.Parse(xml, CancellationToken.None);
        var statement = mapper.Statements[0];

        var root      = Assert.IsType<MixedNode>(statement.RootNode);
        var paramNode = root.Children.OfType<ParameterNode>().First();
        Assert.Equal("id", paramNode.Name);
        Assert.False(paramNode.IsStringSubstitution);
    }

    [Fact]
    public void ParseStringSubstitution() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <select id=""DynamicTable"">
        SELECT * FROM ${tableName}
    </select>
</mapper>";

        var mapper    = XmlMapperParser.Parse(xml, CancellationToken.None);
        var statement = mapper.Statements[0];

        var root      = Assert.IsType<MixedNode>(statement.RootNode);
        var paramNode = root.Children.OfType<ParameterNode>().First();
        Assert.Equal("tableName", paramNode.Name);
        Assert.True(paramNode.IsStringSubstitution);
    }

    [Fact]
    public void ParseInsertStatement() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <insert id=""InsertUser"">
        INSERT INTO users (name, email) VALUES (#{name}, #{email})
    </insert>
</mapper>";

        var mapper = XmlMapperParser.Parse(xml, CancellationToken.None);

        Assert.Single(mapper.Statements);
        Assert.Equal("InsertUser", mapper.Statements[0].Id);
        Assert.Equal("Insert", mapper.Statements[0].StatementType);
    }

    [Fact]
    public void ParseSqlFragment() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <sql id=""userColumns"">id, name, email</sql>
    <select id=""GetAll"">
        SELECT <include refid=""userColumns""/> FROM users
    </select>
</mapper>";

        var mapper = XmlMapperParser.Parse(xml, CancellationToken.None);

        Assert.Single(mapper.SqlFragments);
        Assert.Equal("userColumns", mapper.SqlFragments[0].Id);

        var root = Assert.IsType<MixedNode>(mapper.Statements[0].RootNode);
        var includeNode = root.Children.OfType<IncludeNode>().First();
        Assert.Equal("userColumns", includeNode.RefId);
    }

    [Fact]
    public void ParseStatement_WithTimeout() {
        const string xml = @"
<mapper namespace=""ReportMapper"">
    <select id=""GetMonthlyStats"" timeout=""300"">
        SELECT * FROM monthly_stats
    </select>
</mapper>";

        var mapper = XmlMapperParser.Parse(xml, CancellationToken.None);

        Assert.Single(mapper.Statements);
        Assert.Equal(300, mapper.Statements[0].Timeout);
    }

    [Fact]
    public void ParseStatement_WithoutTimeout_IsNull() {
        const string xml = @"
<mapper namespace=""UserMapper"">
    <select id=""GetUser"">
        SELECT * FROM users
    </select>
</mapper>";

        var mapper = XmlMapperParser.Parse(xml, CancellationToken.None);

        Assert.Null(mapper.Statements[0].Timeout);
    }

    [Fact]
    public void ParseStatement_MultipleStatements_MixedTimeout() {
        const string xml = @"
<mapper namespace=""ReportMapper"">
    <select id=""FastQuery"">
        SELECT 1
    </select>
    <select id=""SlowStats"" timeout=""600"">
        SELECT * FROM heavy_aggregate
    </select>
    <update id=""UpdateCache"" timeout=""120"">
        UPDATE cache SET refreshed = NOW()
    </update>
</mapper>";

        var mapper = XmlMapperParser.Parse(xml, CancellationToken.None);

        Assert.Equal(3, mapper.Statements.Length);
        Assert.Null(mapper.Statements[0].Timeout);
        Assert.Equal(600, mapper.Statements[1].Timeout);
        Assert.Equal(120, mapper.Statements[2].Timeout);
    }

    [Fact]
    public void ParseStatement_InvalidTimeout_IsNull() {
        const string xml = @"
<mapper namespace=""TestMapper"">
    <select id=""BadTimeout"" timeout=""not_a_number"">
        SELECT 1
    </select>
</mapper>";

        var mapper = XmlMapperParser.Parse(xml, CancellationToken.None);

        Assert.Null(mapper.Statements[0].Timeout);
    }
}
