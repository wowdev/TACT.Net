namespace TACT.Net.SystemFiles
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
