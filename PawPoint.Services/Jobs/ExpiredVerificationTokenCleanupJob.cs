using PawPoint.Services.Interfaces;

namespace PawPoint.Services.Jobs
{
    public class ExpiredVerificationTokenCleanupJob(IAuthService auth)
    {
        public async System.Threading.Tasks.Task RunAsync()
        {
            await auth.DeleteExpiredVerificationTokensAsync(
                systemRun: true,
                type: null
            );
        }
    }
}
