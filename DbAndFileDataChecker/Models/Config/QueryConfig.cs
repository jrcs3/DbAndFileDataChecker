public class QueryConfig
{
    public string? Name { get; set; }
    public string? CommandText { get; set; }
    public string? ConnectionString { get; set; }
    public List<ParameterConfig>? Parameters { get; set; }
    public List<ColumnConfig>? Columns { get; set; }
}
