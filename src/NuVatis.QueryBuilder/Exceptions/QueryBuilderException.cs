namespace NuVatis.QueryBuilder.Exceptions;

public class QueryBuilderException : Exception {
    public QueryBuilderException(string message) : base(message) { }
    public QueryBuilderException(string message, Exception inner) : base(message, inner) { }
}

public sealed class SqlRenderException : QueryBuilderException {
    public SqlRenderException(string message) : base(message) { }
    public SqlRenderException(string message, Exception inner) : base(message, inner) { }
}

public sealed class SchemaValidationException : QueryBuilderException {
    public SchemaValidationException(string message) : base(message) { }
}

public sealed class ExecutionException : QueryBuilderException {
    public ExecutionException(string message, Exception inner) : base(message, inner) { }
}
