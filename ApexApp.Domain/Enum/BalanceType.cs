using NpgsqlTypes;

public enum BalanceType
{
    [PgName("personal")]
    Personal,

    [PgName("reward")]
    Reward
}