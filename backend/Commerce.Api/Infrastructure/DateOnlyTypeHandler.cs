using System.Data;
using Dapper;

namespace Commerce.Api.Infrastructure;

/// <summary>Permite a Dapper leer/escribir columnas DATE de SQL Server como DateOnly.</summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}
