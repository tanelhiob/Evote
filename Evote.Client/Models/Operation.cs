namespace Evote.Client.Models
{
    public partial class Operation<TSuccess, TFailure>
    {
        public OperationStatus Status { get; set; } = OperationStatus.NotStarted;
        public TSuccess Success { get; set; }
        public TFailure Failure { get; set; }

        public void Start()
        {
            Success = default;
            Failure = default;
            Status = OperationStatus.InProgress;
        }

        public void Fail(TFailure failure)
        {
            Failure = failure;
            Status = OperationStatus.Failed;
        }

        public void Complete(TSuccess success)
        {
            Success = success;
            Status = OperationStatus.Successful;
        }
    }
}
