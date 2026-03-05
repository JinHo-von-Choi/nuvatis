#nullable enable
using System.Collections.Immutable;
using NuVatis.Generators.Emitters;
using NuVatis.Generators.Models;
using Xunit;

namespace NuVatis.Generators.Tests;

/**
 * ParameterEmitter.EmitDynamicBuilderLambda 포괄적 단위 테스트.
 * 모든 동적 SQL 노드 타입(foreach, if, where, set, choose, ${})에 대해
 * 생성된 람다 소스 코드가 올바른 구조를 갖는지 검증한다.
 *
 * @author 최진호
 * @date   2026-03-06
 */
public class ParameterEmitterDynamicBuilderTests {

    // ─────────────────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────────────────

    private static string Lambda(ParsedSqlNode root, string prefix = "@")
        => ParameterEmitter.EmitDynamicBuilderLambda(root, prefix);

    private static MixedNode Mixed(params ParsedSqlNode[] children)
        => new(ImmutableArray.Create(children));

    private static TextNode Text(string t)    => new(t);
    private static ParameterNode Param(string name)    => new(name, false);
    private static ParameterNode StrSub(string name)   => new(name, true);

    private static IfNode If(string test, params ParsedSqlNode[] children)
        => new(test, ImmutableArray.Create(children));

    private static ForEachNode ForEach(
        string coll, string item,
        string? open = null, string? close = null, string? sep = null,
        params ParsedSqlNode[] children)
        => new(coll, item, open, close, sep, ImmutableArray.Create(children));

    private static WhereNode Where(params ParsedSqlNode[] children)
        => new(ImmutableArray.Create(children));

    private static SetNode Set(params ParsedSqlNode[] children)
        => new(ImmutableArray.Create(children));

    private static ChooseNode Choose(
        WhenClause[] whens,
        ParsedSqlNode[]? otherwise = null) {

        var otherwiseArr = otherwise is null
            ? (ImmutableArray<ParsedSqlNode>?)null
            : ImmutableArray.Create(otherwise);
        return new ChooseNode(ImmutableArray.Create(whens), otherwiseArr);
    }

    private static WhenClause When(string test, params ParsedSqlNode[] children)
        => new(test, ImmutableArray.Create(children));

    // ─────────────────────────────────────────────────────────────────────────
    // 1. 람다 기본 구조
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Lambda_ContainsRequiredBoilerplate() {
        var code = Lambda(Text("SELECT 1"));

        Assert.Contains("static (__param_) =>", code);
        Assert.Contains("__sb_", code);
        Assert.Contains("__params_", code);
        Assert.Contains("__idx_", code);
        Assert.Contains("__getprop_", code);
        Assert.Contains("return (__sb_.ToString(), __params_)", code);
    }

    [Fact]
    public void Lambda_GetpropHelper_UsesIgnoreCaseBindingFlags() {
        var code = Lambda(Text("SELECT 1"));

        Assert.Contains("BindingFlags.IgnoreCase", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. TextNode
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TextNode_AppendsLiteralToBuilder() {
        var code = Lambda(Text("SELECT id FROM users"));

        Assert.Contains("__sb_.Append(@\"SELECT id FROM users\")", code);
    }

    [Fact]
    public void TextNode_WhitespaceOnly_IsSkipped() {
        var code = Lambda(Mixed(Text("  \n  "), Text("SELECT 1")));

        // 공백 전용 노드는 Append 호출을 생성하지 않아야 한다.
        Assert.DoesNotContain("Append(@\"  \n  \")", code);
        Assert.Contains("SELECT 1", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. ParameterNode (#{...})
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParameterNode_Simple_GeneratesGetpropAndCreateParameter() {
        var code = Lambda(Mixed(Text("WHERE id = "), Param("id")));

        Assert.Contains("__getprop_(__param_, \"id\")", code);
        Assert.Contains("ParameterBinder.CreateParameter", code);
        Assert.Contains("__idx_++", code);
    }

    [Fact]
    public void ParameterNode_NestedOneDot_ChainsGetprop() {
        // #{user.Name} → __getprop_(__getprop_(__param_, "user"), "Name")
        var code = Lambda(Param("user.Name"));

        Assert.Contains("__getprop_(__getprop_(__param_, \"user\"), \"Name\")", code);
    }

    [Fact]
    public void ParameterNode_NestedTwoDots_DoubleChainedGetprop() {
        // #{user.Address.City} → __getprop_(__getprop_(__getprop_(__param_, "user"), "Address"), "City")
        var code = Lambda(Param("user.Address.City"));

        Assert.Contains("__getprop_(__getprop_(__getprop_(__param_, \"user\"), \"Address\"), \"City\")", code);
    }

    [Fact]
    public void ParameterNode_UsesPrefixForParameterName() {
        var code = Lambda(Param("id"), prefix: ":");

        Assert.Contains("\":p\" + __idx_++", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. ParameterNode (${}  SqlIdentifier 가드)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void StringSubstitution_GeneratesSqlIdentifierTypeCheck() {
        var code = Lambda(Mixed(Text("SELECT * FROM "), StrSub("tableName")));

        Assert.Contains("SqlIdentifier", code);
        Assert.Contains("InvalidOperationException", code);
        Assert.Contains("ToString()", code);
    }

    [Fact]
    public void StringSubstitution_DoesNotGenerateCreateParameter() {
        var code = Lambda(StrSub("tableName"));

        // ${}는 SqlIdentifier.ToString()으로 SQL에 직접 삽입, DbParameter 생성 없음
        Assert.DoesNotContain("ParameterBinder.CreateParameter", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. ForEachNode
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ForEach_ScalarItems_GeneratesLoopAndBinding() {
        var node = ForEach("ids", "id", "(", ")", ",", Param("id"));
        var code = Lambda(node);

        Assert.Contains("__getprop_(__param_, \"ids\") as System.Collections.IEnumerable", code);
        Assert.Contains("foreach (var id_ in", code);
        // 스칼라 아이템: itemVar 자체가 값
        Assert.Contains("ParameterBinder.CreateParameter", code);
    }

    [Fact]
    public void ForEach_OpenCloseSeparator_EmittedCorrectly() {
        var node = ForEach("ids", "id", "(", ")", ", ", Param("id"));
        var code = Lambda(node);

        Assert.Contains("__sb_.Append(@\"(\")", code);
        Assert.Contains("__sb_.Append(@\")\")", code);
        Assert.Contains("__sb_.Append(@\", \")", code);
    }

    [Fact]
    public void ForEach_NoOpenClose_DoesNotEmitBrackets() {
        var node = ForEach("ids", "id", null, null, null, Param("id"));
        var code = Lambda(node);

        // open/close가 null이면 Append("(") 생성 없음
        Assert.DoesNotContain("__sb_.Append(@\"(\")", code);
    }

    [Fact]
    public void ForEach_NestedProperty_GeneratesGetpropAccess() {
        // #{user.UserName} → __getprop_(user_, "UserName")
        var node = ForEach("users", "user", "(", ")", ",", Param("user.UserName"));
        var code = Lambda(node);

        Assert.Contains("foreach (var user_ in", code);
        Assert.Contains("__getprop_(user_, \"UserName\")", code);
        Assert.Contains("ParameterBinder.CreateParameter", code);
    }

    [Fact]
    public void ForEach_DeepNestedProperty_GeneratesChainedGetprop() {
        // #{user.Address.City} → __getprop_(__getprop_(user_, "Address"), "City")
        var node = ForEach("users", "user", null, null, null, Param("user.Address.City"));
        var code = Lambda(node);

        Assert.Contains("__getprop_(__getprop_(user_, \"Address\"), \"City\")", code);
    }

    [Fact]
    public void ForEach_FirstVarManagesFirstFlag() {
        var node = ForEach("ids", "id", null, null, ",", Param("id"));
        var code = Lambda(node);

        // __first_ids_ 변수 초기화 및 토글 패턴 확인
        Assert.Contains("__first_ids_", code);
        Assert.Contains("= true", code);
        Assert.Contains("= false", code);
    }

    [Fact]
    public void ForEach_StringSubstitution_InsideForeach_GeneratesSqlIdentifierGuard() {
        var node = ForEach("cols", "col", null, null, ",", StrSub("col"));
        var code = Lambda(node);

        Assert.Contains("SqlIdentifier", code);
        Assert.Contains("InvalidOperationException", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. IfNode
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IfNode_GeneratesNullCheckConditional() {
        var node = If("name != null", Text("AND name = "), Param("name"));
        var code = Lambda(node);

        Assert.Contains("__getprop_(__param_, \"name\") != null", code);
        Assert.Contains("AND name = ", code);
        Assert.Contains("\"name\"", code);
    }

    [Fact]
    public void IfNode_ExtractsPropertyFromComplexTest() {
        // "age > 0" → 프로퍼티명 "age" 추출
        var node = If("age > 0", Text("AND age > 0"));
        var code = Lambda(node);

        Assert.Contains("__getprop_(__param_, \"age\") != null", code);
    }

    [Fact]
    public void IfNode_BodyIsIndented() {
        var node = Mixed(
            Text("SELECT * FROM t"),
            If("id != null", Text("WHERE id = "), Param("id"))
        );
        var code = Lambda(node);

        Assert.Contains("if (__getprop_(__param_, \"id\") != null)", code);
        Assert.Contains("WHERE id = ", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. WhereNode
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WhereNode_GeneratesWhereKeyword() {
        var node = Where(If("id != null", Text("AND id = "), Param("id")));
        var code = Lambda(node);

        Assert.Contains("WHERE", code);
    }

    [Fact]
    public void WhereNode_TrimsLeadingAnd() {
        var node = Where(Text("AND name = 'test'"));
        var code = Lambda(node);

        Assert.Contains("StartsWith(\"AND \"", code);
        Assert.Contains("Substring(4)", code);
    }

    [Fact]
    public void WhereNode_TrimsLeadingOr() {
        var node = Where(Text("OR status = 1"));
        var code = Lambda(node);

        Assert.Contains("StartsWith(\"OR \"", code);
        Assert.Contains("Substring(3)", code);
    }

    [Fact]
    public void WhereNode_UsesSeparateStringBuilder() {
        var node = Where(If("name != null", Text("AND name = "), Param("name")));
        var code = Lambda(node);

        // 임시 StringBuilder를 사용해야 한다 (__wSb_)
        Assert.Contains("__wSb_", code);
        Assert.Contains("__outerSb_", code);
    }

    [Fact]
    public void WhereNode_MultipleIf_AllConditionsPresent() {
        var node = Where(
            If("name != null", Text("AND name = "), Param("name")),
            If("age != null",  Text("AND age = "),  Param("age"))
        );
        var code = Lambda(node);

        Assert.Contains("\"name\"", code);
        Assert.Contains("\"age\"", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. SetNode
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SetNode_GeneratesSetKeyword() {
        var node = Set(If("name != null", Text("name = "), Param("name"), Text(",")));
        var code = Lambda(node);

        Assert.Contains("SET", code);
    }

    [Fact]
    public void SetNode_TrimsTrailingComma() {
        var node = Set(Text("name = 'test',"));
        var code = Lambda(node);

        Assert.Contains("EndsWith(\",\")", code);
        Assert.Contains("Substring(0", code);
    }

    [Fact]
    public void SetNode_UsesSeparateStringBuilder() {
        var node = Set(Text("name = 'test'"));
        var code = Lambda(node);

        Assert.Contains("__sSb_", code);
        Assert.Contains("__outerSb_", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. ChooseNode
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ChooseNode_SingleWhen_GeneratesIfBlock() {
        var node = Choose(
            new[] { When("type != null", Text("type_a")) }
        );
        var code = Lambda(node);

        Assert.Contains("if (", code);
        Assert.Contains("\"type\"", code);
        Assert.Contains("type_a", code);
    }

    [Fact]
    public void ChooseNode_MultipleWhens_GeneratesIfElseIfChain() {
        var node = Choose(
            new[] {
                When("typeA != null", Text("branch_a")),
                When("typeB != null", Text("branch_b"))
            }
        );
        var code = Lambda(node);

        Assert.Contains("if (", code);
        Assert.Contains("else if (", code);
        Assert.Contains("branch_a", code);
        Assert.Contains("branch_b", code);
    }

    [Fact]
    public void ChooseNode_WithOtherwise_GeneratesElseBlock() {
        var node = Choose(
            new[] { When("type != null", Text("branch_a")) },
            new ParsedSqlNode[] { Text("default_branch") }
        );
        var code = Lambda(node);

        Assert.Contains("else", code);
        Assert.Contains("default_branch", code);
    }

    [Fact]
    public void ChooseNode_WithoutOtherwise_NoElseBlock() {
        var node = Choose(
            new[] { When("type != null", Text("branch_a")) }
        );
        var code = Lambda(node);

        // otherwise 없으면 단독 else 블록 생성 없음
        Assert.DoesNotContain("default_branch", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. MixedNode — 복합 시나리오
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Complex_WhereWithMultipleIf_AllBranchesPresent() {
        var node = Mixed(
            Text("SELECT id, name FROM users"),
            Where(
                If("name != null", Text("AND name = "), Param("name")),
                If("status != null", Text("AND status = "), Param("status"))
            )
        );
        var code = Lambda(node);

        Assert.Contains("SELECT id, name FROM users", code);
        Assert.Contains("WHERE", code);
        Assert.Contains("__getprop_(__param_, \"name\")", code);
        Assert.Contains("__getprop_(__param_, \"status\")", code);
    }

    [Fact]
    public void Complex_InsertWithForeachNestedProperty() {
        var node = Mixed(
            Text("INSERT INTO users (name, email) VALUES "),
            ForEach("users", "user", "(", ")", ",",
                Text("("),
                Param("user.Name"),
                Text(", "),
                Param("user.Email"),
                Text(")")
            )
        );
        var code = Lambda(node);

        Assert.Contains("INSERT INTO users", code);
        Assert.Contains("foreach (var user_ in", code);
        Assert.Contains("__getprop_(user_, \"Name\")", code);
        Assert.Contains("__getprop_(user_, \"Email\")", code);
    }

    [Fact]
    public void Complex_UpdateWithSetNode() {
        var node = Mixed(
            Text("UPDATE users"),
            Set(
                If("name != null",   Text("name = "),   Param("name"),   Text(",")),
                If("email != null",  Text("email = "),  Param("email"),  Text(","))
            ),
            Text("WHERE id = "),
            Param("id")
        );
        var code = Lambda(node);

        Assert.Contains("UPDATE users", code);
        Assert.Contains("SET", code);
        Assert.Contains("\"name\"", code);
        Assert.Contains("\"email\"", code);
        Assert.Contains("WHERE id = ", code);
    }

    [Fact]
    public void Complex_BulkInsertWithFlatScalar() {
        // INSERT INTO t (v) VALUES <foreach collection="ids" item="id" open="(" close=")" separator=",">#{id}</foreach>
        var node = Mixed(
            Text("INSERT INTO t (v) VALUES "),
            ForEach("ids", "id", "(", ")", ",", Param("id"))
        );
        var code = Lambda(node);

        Assert.Contains("INSERT INTO t (v) VALUES ", code);
        Assert.Contains("__getprop_(__param_, \"ids\")", code);
        Assert.Contains("foreach (var id_ in", code);
        Assert.Contains("ParameterBinder.CreateParameter", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. XML 파서 통합 — XmlMapperParser로 파싱 후 람다 생성
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void XmlParser_StaticSelect_FlattenedCorrectly() {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.IMapper"">
  <select id=""GetById"">
    SELECT id, name FROM users WHERE id = #{id}
  </select>
</mapper>";
        var mapper = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xml, default);
        var stmt   = mapper.Statements[0];

        // 정적 SQL: 동적 노드 없음
        Assert.False(DynamicSqlEmitter.HasDynamicNodes(stmt.RootNode));
    }

    [Fact]
    public void XmlParser_ForeachInsert_ParsedAsDynamic() {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.IMapper"">
  <insert id=""BulkInsert"">
    INSERT INTO users (name) VALUES
    <foreach collection=""users"" item=""user"" open=""("" close="")"" separator="","">
      #{user.UserName}
    </foreach>
  </insert>
</mapper>";
        var mapper = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xml, default);
        var stmt   = mapper.Statements[0];

        Assert.True(DynamicSqlEmitter.HasDynamicNodes(stmt.RootNode));

        var code = Lambda(stmt.RootNode);

        Assert.Contains("foreach (var user_ in", code);
        Assert.Contains("__getprop_(user_, \"UserName\")", code);
    }

    [Fact]
    public void XmlParser_WhereIfSelect_ParsedAndLambdaCorrect() {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.IMapper"">
  <select id=""Search"">
    SELECT id FROM users
    <where>
      <if test=""name != null"">AND name = #{name}</if>
      <if test=""email != null"">AND email = #{email}</if>
    </where>
  </select>
</mapper>";
        var mapper = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xml, default);
        var stmt   = mapper.Statements[0];

        Assert.True(DynamicSqlEmitter.HasDynamicNodes(stmt.RootNode));

        var code = Lambda(stmt.RootNode);

        Assert.Contains("WHERE", code);
        Assert.Contains("\"name\"", code);
        Assert.Contains("\"email\"", code);
    }

    [Fact]
    public void XmlParser_ChooseWhenOtherwise_ParsedAndLambdaCorrect() {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.IMapper"">
  <select id=""GetStatus"">
    SELECT *
    <choose>
      <when test=""status != null"">WHERE status = #{status}</when>
      <otherwise>WHERE status = 'active'</otherwise>
    </choose>
  </select>
</mapper>";
        var mapper = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xml, default);
        var stmt   = mapper.Statements[0];

        Assert.True(DynamicSqlEmitter.HasDynamicNodes(stmt.RootNode));

        var code = Lambda(stmt.RootNode);

        Assert.Contains("if (", code);
        Assert.Contains("else", code);
        Assert.Contains("status = 'active'", code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12. RegisterXmlStatements — Registry 생성 확인
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RegistryEmitter_StaticStatement_UsesSqlSource() {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.IMapper"">
  <select id=""GetAll"">SELECT id FROM users</select>
</mapper>";
        var mapper  = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xml, default);
        var mappers = System.Collections.Immutable.ImmutableArray.Create(mapper);

        // 빈 인터페이스 배열로 Registry 생성 (xml mappers만 확인)
        var registry = RegistryEmitter.Emit(
            System.Collections.Immutable.ImmutableArray<NuVatis.Generators.Analysis.MapperInterfaceInfo>.Empty,
            mappers);

        Assert.Contains("RegisterXmlStatements", registry);
        Assert.Contains("\"GetAll\"", registry);
        // 정적 SQL → SqlSource에 직접
        Assert.Contains("SELECT id FROM users", registry);
        // DynamicSqlBuilder 없음
        Assert.DoesNotContain("DynamicSqlBuilder", registry);
    }

    [Fact]
    public void RegistryEmitter_DynamicStatement_UsesDynamicSqlBuilder() {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.IMapper"">
  <insert id=""BulkInsert"">
    INSERT INTO t (v) VALUES
    <foreach collection=""ids"" item=""id"" open=""("" close="")"" separator="","">#{id}</foreach>
  </insert>
</mapper>";
        var mapper  = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xml, default);
        var mappers = System.Collections.Immutable.ImmutableArray.Create(mapper);

        var registry = RegistryEmitter.Emit(
            System.Collections.Immutable.ImmutableArray<NuVatis.Generators.Analysis.MapperInterfaceInfo>.Empty,
            mappers);

        Assert.Contains("\"BulkInsert\"", registry);
        Assert.Contains("DynamicSqlBuilder", registry);
        Assert.Contains("SqlSource = \"\"", registry);
        Assert.Contains("__getprop_", registry);
    }

    [Fact]
    public void RegistryEmitter_ResultMapId_IncludedInStatement() {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.IMapper"">
  <resultMap id=""userMap"" type=""User"">
    <id column=""id"" property=""Id""/>
  </resultMap>
  <select id=""GetById"" resultMap=""userMap"">
    SELECT id FROM users WHERE id = #{id}
  </select>
</mapper>";
        var mapper  = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xml, default);
        var mappers = System.Collections.Immutable.ImmutableArray.Create(mapper);

        var registry = RegistryEmitter.Emit(
            System.Collections.Immutable.ImmutableArray<NuVatis.Generators.Analysis.MapperInterfaceInfo>.Empty,
            mappers);

        Assert.Contains("ResultMapId = \"userMap\"", registry);
    }

    [Fact]
    public void RegistryEmitter_StatementType_CapitalizedCorrectly() {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<mapper namespace=""TestApp.IMapper"">
  <select id=""S"">SELECT 1</select>
  <insert id=""I"">INSERT INTO t VALUES (1)</insert>
  <update id=""U"">UPDATE t SET v=1</update>
  <delete id=""D"">DELETE FROM t</delete>
</mapper>";
        var mapper  = NuVatis.Generators.Parsing.XmlMapperParser.Parse(xml, default);
        var mappers = System.Collections.Immutable.ImmutableArray.Create(mapper);

        var registry = RegistryEmitter.Emit(
            System.Collections.Immutable.ImmutableArray<NuVatis.Generators.Analysis.MapperInterfaceInfo>.Empty,
            mappers);

        Assert.Contains("StatementType.Select", registry);
        Assert.Contains("StatementType.Insert", registry);
        Assert.Contains("StatementType.Update", registry);
        Assert.Contains("StatementType.Delete", registry);
    }

    [Fact]
    public void RegistryEmitter_NoXmlMappers_NoRegisterXmlStatements() {
        var registry = RegistryEmitter.Emit(
            System.Collections.Immutable.ImmutableArray<NuVatis.Generators.Analysis.MapperInterfaceInfo>.Empty);

        // xmlMappers = default → RegisterXmlStatements 생성 안 됨
        Assert.DoesNotContain("RegisterXmlStatements", registry);
    }
}
