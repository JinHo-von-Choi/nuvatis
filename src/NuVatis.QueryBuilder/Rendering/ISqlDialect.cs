namespace NuVatis.QueryBuilder.Rendering;

using NuVatis.QueryBuilder.Ast;

public interface ISqlDialect {
    RenderedSql Render(SelectQuery query);
    RenderedSql Render(InsertQuery query);
    RenderedSql Render(UpdateQuery query);
    RenderedSql Render(DeleteQuery query);

    string QuoteIdentifier(string name);
    string Placeholder(int index);
}
