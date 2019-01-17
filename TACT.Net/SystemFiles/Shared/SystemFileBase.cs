namespace TACT.Net.SystemFiles.Shared
{
    public class SystemFileBase
    {
        protected readonly TACT Container;

        public SystemFileBase(TACT container)
        {
            Container = container;
            Container?.Inject(this);
        }
    }
}
