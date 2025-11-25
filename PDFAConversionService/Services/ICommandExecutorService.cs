namespace PDFAConversionService.Services
{
    public interface ICommandExecutorService
    {
        public (int ExitCode, string Output) RunCommand(string fileName, string arguments, int? timeoutInSeconds = null);
    }
}
