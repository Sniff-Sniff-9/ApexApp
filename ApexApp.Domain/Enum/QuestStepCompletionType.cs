using NpgsqlTypes;

public enum QuestStepCompletionType
{
    [PgName("manual")]
    Manual,

    [PgName("proof")]
    Proof,

    [PgName("automatic")]
    Automatic
}