namespace IronJade.UI.Core
{
    public class BaseModel
    {
        public bool IsStack { get; protected set; } = true;

        public void SetStack(bool isStack)
        {
            IsStack = isStack;
        }
    }
}