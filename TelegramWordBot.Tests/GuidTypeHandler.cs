using System.Data;
using Dapper;

public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value)
    {
        if (value is byte[] bytes)
            return new Guid(bytes);
        return Guid.Parse(value.ToString()!);
    }

    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = value.ToString();
    }
}
