
public static class ExecutionOrder
{
    public const int SYSTEM_LAST = 10000;
    public const int SLOWEST = 1000;
    public const int SLOWER = 100;
    public const int SLOW = 10;
    public const int DEFAULT = 0;
    public const int FAST = -10;
    public const int FASTER = -100;
    public const int FASTEST = -1000;
    public const int SYSTEM_INIT = -10000;
    
    // define custom order like below
    // public const int A_BIT_MORE_FAST = FAST - 1;
}