    public interface IPool
    {
        Response<bool> PoolExists(string poolName);
        Task<Response<bool>> StartPool(string poolName);
        Task<Response<bool>> StopPool(string poolName);
        Task<Response<bool>> RecyclePool(string poolName);
        Task<Response<bool>> CreatePool(string poolName, bool isNetCore = false);
        Task<Response<string>> PoolStatus(string poolName);
    }
