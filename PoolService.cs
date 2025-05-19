Server.Management
{

    public class PoolService : IPool, IDisposable
    {
        private readonly ServerManager serverManager;
        public PoolService(string? configPath = null)
        {
            // If there is a config path defined.
            if (!configPath.IsBlank())
            {
                ConfigPath = configPath;
            }

            serverManager = GetServerManager();
        }

   
        public string ConfigPath { get; set; } = string.Empty;

   
        public string WebserverRoot { get; set; } = @"C:\inetpub\wwwroot";

  
        public string ManagedRuntimeVersion { get; set; } = "v4.0";

  
        public int PoolDelay { get; set; } = 10;


        public void Dispose()
        {
            serverManager.Dispose();
        }

        public Response<List<Binding>> SitesBindingsList()
        {
            try
            {
                List<Binding> bindingList = GetAllBindings();



                return Response<List<Binding>>.CreateResponse(bindingList, Response<List<Binding>>.GetMessage(ResponseMessageType.BindingsRetrieved)
                );
            }
            catch (Exception exception)
            {
                return new FailureResponse<List<Binding>>($"{GetMessage(ResponseMessageType.BindingRetrievalError)}");
            }
        }

        public Response<Site> GetSite(string siteName = "Default Web Site")
        {
            try
            {
                Site site = serverManager.Sites[siteName];

                if (site.IsBlank())
                {
                    return new FailureResponse<Site>($"{GetMessage(ResponseMessageType.SiteNotFound)}");
                }

                return new SuccessResponse<Site>(site, $"{GetMessage(ResponseMessageType.SiteRetrieved)}");
            }
            catch (Exception exception)
            {
                exception.Data.Add("SiteName", siteName);

                return new FailureResponse<Site>($"{GetMessage(ResponseMessageType.SiteRetrievalError)}");
            }
        }

        public Response<bool> PoolExists(string poolName)
        {
            try
            {
                if (!CheckValidPoolName(poolName))
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.InvalidPoolName)}");
                }

                bool exists = serverManager.ApplicationPools[poolName] != null;

                if (exists)
                {
                    return new SuccessResponse<bool>(exists, $"{GetMessage(ResponseMessageType.PoolExists)}");
                }
                else
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                } 
            }
            catch (Exception exception)
            {
                return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolExistsCheckError)} : {exception.Message}", exception.StackTrace, exception);
            }
        }


        public async Task<Response<bool>> StartPool(string poolName)
        {
            try
            {
                if (!PoolExists(poolName).Success)
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                ApplicationPool pool = serverManager.ApplicationPools[poolName];

                if (pool.IsBlank())
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                // Wait briefly before starting.
                await Task.Delay(1000);

                await RetryPoolOperationAsync(() => pool.Start());

                serverManager.CommitChanges();

                // Confirm pool started, with timeout.
                Stopwatch stopwatch = Stopwatch.StartNew();

                // Wait for up to 10 seconds for the applications pool to show as exObjectState.
                while (pool.State != ObjectState.Started)
                {
                    TimeoutCheck(stopwatch, poolName, "start");

                    await Task.Delay(500);
                }
                stopwatch.Stop();

                return new SuccessResponse<bool>(true, $"{GetMessage(ResponseMessageType.PoolStartSuccess)}");
            }
            catch (Exception exception)
            {
                return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolStartError)}: {exception.Message}", exception.StackTrace, exception);
            }
        }

        public async Task<Response<bool>> StopPool(string poolName)
        {
            try
            {
                if (!PoolExists(poolName).Success)
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                ApplicationPool pool = serverManager.ApplicationPools[poolName];

                if (pool.IsBlank())
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                // Wait briefly before stopping.
                await Task.Delay(1000);

                if (pool.State == ObjectState.Started)
                {
                    await RetryPoolOperationAsync(() => pool.Stop());
                }

                serverManager.CommitChanges();

                // Confirm pool started, with timeout.
                Stopwatch stopwatch = Stopwatch.StartNew();

                // Wait for up to 10 seconds for the applications pool to show as exObjectState.
                while (pool.State != ObjectState.Stopped)
                {
                    TimeoutCheck(stopwatch, poolName, "stop");

                    await Task.Delay(500);
                }
                stopwatch.Stop();

                return new SuccessResponse<bool>(true, $"{GetMessage(ResponseMessageType.PoolStopSuccess)}");
            }
            catch (Exception exception)
            {
                return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolStopError)}: {exception.Message}", exception.StackTrace, exception);
            }
        }

        public async Task<Response<bool>> RecyclePool(string poolName)
        {
            try
            {
                if (!PoolExists(poolName).Success)
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                ApplicationPool pool = serverManager.ApplicationPools[poolName];

                if (pool.IsBlank())
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                Process process = null;

                if (pool.WorkerProcesses.Count > 0)
                {
                    int processId = pool.WorkerProcesses[0].ProcessId;

                    process = Process.GetProcessById(processId);

                    await RetryPoolOperationAsync(() => pool.Recycle());
                }

                if (!process.IsBlank())
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    while (!process.HasExited)
                    {
                        TimeoutCheck(stopwatch, poolName, "recycle");

                        await Task.Delay(500);
                    }

                    stopwatch.Stop();

                    process.Dispose();
                }

                return new SuccessResponse<bool>(true, $"{GetMessage(ResponseMessageType.PoolRecycleSuccess)}");
            }
            catch (Exception exception)
            {
                return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolRecycleError)}: {exception.Message}", exception.StackTrace, exception);
            }
        }


        public async Task<Response<bool>> CreatePool(string poolName, bool isNetCore = false)
        {
            try
            {
                if (PoolExists(poolName).Success)
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolExists)}");
                }

                ApplicationPool newPool = serverManager.ApplicationPools.Add(poolName);

                newPool.ManagedRuntimeVersion = isNetCore ? string.Empty : "v4.0";
                newPool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
                newPool.AutoStart = true;

                newPool.ProcessModel.IdentityType = ProcessModelIdentityType.ApplicationPoolIdentity;
                newPool.ProcessModel.IdleTimeout = TimeSpan.FromMinutes(240);
                newPool.ProcessModel.MaxProcesses = 1;
                newPool.Recycling.PeriodicRestart.Time = TimeSpan.FromHours(24);

                serverManager.CommitChanges();

                return new SuccessResponse<bool>(true, $"{GetMessage(ResponseMessageType.PoolCreateSuccess)}");
            }
            catch (Exception exception)
            {
                return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolCreateError)}: {exception.Message}", exception.StackTrace, exception);
            }
        }

      
        public async Task<Response<bool>> CreatePool(string poolName, string userName, string password, bool isNetCore)
        {
            try
            {
                if (PoolExists(poolName).Success)
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolExists)}");
                }

                Response<UserPrincipal?> userResponse = GetPrincipalUser(userName);

                if (!userResponse.Success || userResponse.Result == null || userResponse.Result.Enabled == false)
                {
                    return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.UserNotFound)}");
                }

                ApplicationPool newPool = serverManager.ApplicationPools.Add(poolName);

                newPool.ManagedRuntimeVersion = isNetCore ? "" : "v4.0";
                newPool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
                newPool.AutoStart = true;

                newPool.ProcessModel.IdentityType = ProcessModelIdentityType.SpecificUser;
                newPool.ProcessModel.UserName = userName;
                newPool.ProcessModel.Password = password;

                newPool.ProcessModel.IdleTimeout = TimeSpan.FromMinutes(240);
                newPool.ProcessModel.MaxProcesses = 1;
                newPool.Recycling.PeriodicRestart.Time = TimeSpan.FromHours(24);

                serverManager.CommitChanges();

                return new SuccessResponse<bool>(true, $"{GetMessage(ResponseMessageType.PoolCreateSuccess)}");
            }
            catch (Exception exception)
            {
                return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.PoolCreateError)}: {exception.Message}", exception.StackTrace, exception);
            }
        }

        public async Task<Response<string>> PoolStatus(string poolName)
        {
            try
            {
                if (!PoolExists(poolName).Success)
                {
                    return new FailureResponse<string>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                ApplicationPool pool = serverManager.ApplicationPools[poolName];

                if (pool.IsBlank())
                {
                    return new FailureResponse<string>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                string status = pool.State.ToString();

                return new SuccessResponse<string>(status, $"{GetMessage(ResponseMessageType.PoolStatusRetrieved)}");
            }
            catch (Exception exception)
            {
                return new FailureResponse<string>($"{GetMessage(ResponseMessageType.PoolStatusError)}: {exception.Message}", exception.StackTrace, exception);
            }
        }

        private Response<ApplicationPool> GetApplicationPool(string poolName)
        {
            try
            {
                if (!PoolExists(poolName).Success)
                {
                    return new FailureResponse<ApplicationPool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                ApplicationPool pool = serverManager.ApplicationPools[poolName];

                if (pool.IsBlank())
                {
                    return new FailureResponse<ApplicationPool>($"{GetMessage(ResponseMessageType.PoolDoesNotExist)}");
                }

                return new SuccessResponse<ApplicationPool>(pool, $"{GetMessage(ResponseMessageType.PoolRetrievalSuccess)}");
            }
            catch (Exception exception)
            {
                return new FailureResponse<ApplicationPool>($"{GetMessage(ResponseMessageType.PoolRetrievalError)}: {exception.Message}", exception.StackTrace, exception);
            }
        }

        private bool CheckValidPoolName(string poolName)
        {
            if (poolName.IsBlank())
            {
                return false;
            }

            return true;
        }
        private Response<UserPrincipal?> FindUserInDomain(string userName, string domain,string user )
        {
            // Attempt to connect to the domain and find the user there.
            using PrincipalContext domainContext = new PrincipalContext(ContextType.Domain, domain);

            UserPrincipal domainUser = UserPrincipal.FindByIdentity(domainContext, IdentityType.SamAccountName, user);

            if (!domainUser.IsBlank())
            {
                return new SuccessResponse<UserPrincipal?>(domainUser, $"User '{userName}' found in domain context.");
            }
            return new FailureResponse<UserPrincipal?>($"{GetMessage(ResponseMessageType.UserNotFound)}");
        }

        private Response<UserPrincipal?> FindUserInLocal(string userName, string domain, string user)
        {
            try
            {
                using PrincipalContext machineContext = new PrincipalContext(ContextType.Machine, domain);

                UserPrincipal localUser = UserPrincipal.FindByIdentity(machineContext, IdentityType.SamAccountName, user);

                if (!localUser.IsBlank())
                {
                    return new SuccessResponse<UserPrincipal?>(localUser, $"User '{userName}' found in local machine context.");
                }

                return new FailureResponse<UserPrincipal?>($"{GetMessage(ResponseMessageType.UserNotFound)}");
            }
            catch (Exception machineException)
            {
                return new FailureResponse<UserPrincipal?>($"{GetMessage(ResponseMessageType.UserRetrievalError)}: { machineException.Message}", machineException.StackTrace, machineException);
            }
        }
        private Response<UserPrincipal?> GetPrincipalUser(string userName)
        {
            // Validate the input format: must not be blank and must include a backslash.
            if (userName.IsBlank() || !userName.Contains("\\"))
            {
                return new FailureResponse<UserPrincipal?>(GetMessage(ResponseMessageType.InvalidUsernameFormat));
            }

            // Split into domain and username parts.
            string[] parts = @userName.Split('\\');

            // Ensure exactly two components: DOMAIN and Username.
            if (parts.Length != 2)
            {
                return new FailureResponse<UserPrincipal?>(GetMessage(ResponseMessageType.InvalidUsernameFormat));
            }

            string domain = parts[0];
            string user = parts[1];

            try
            {
                // Attempt to connect to the domain and find the user there.
                return FindUserInDomain(userName, domain, user);
            }
            catch (PrincipalServerDownException)
            {
                // Domain controller unreachable — fall back to local machine context.
                return FindUserInLocal(userName, domain, user);
            }
            catch (Exception exception)
            {
                return new FailureResponse<UserPrincipal?>($"{GetMessage(ResponseMessageType.UserRetrievalError)}");
            }
        }
        private async Task RetryPoolOperationAsync(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                await Task.Delay(1000 * PoolDelay);

                action();
            }
        }

        private Response<bool>? TimeoutCheck(Stopwatch stopwatch, string poolName, string action)
        {
            if (stopwatch.ElapsedMilliseconds > 1000 * PoolDelay)
            {
                return new FailureResponse<bool>($"{GetMessage(ResponseMessageType.Timeout)} {poolName} {action}");
            }

            return null;
        }


        private ServerManager GetServerManager()
        {
            // If there is a config path defined.
            if (!ConfigPath.IsBlank())
            {
                // Override the default object config path.
                return new ServerManager(ConfigPath);
            }

            return new ServerManager();
        }

        private List<Binding> GetAllBindings()
        {
            List<Binding> bindingList = new List<Binding>();

            foreach (Site site in serverManager.Sites)
            {
                foreach (Binding binding in site.Bindings)
                {
                    bindingList.Add(binding);
                }
            }

            return bindingList;
        }
    }
}
