namespace Nop.Web.Factories;

/// <summary>
/// Represents the factory to prepare llms.txt content
/// </summary>
public partial interface ILlmsModelFactory
{
    /// <summary>
    /// Prepare llms.txt content
    /// </summary>
    /// <param name="full">A value indicating whether to generate full content (llms-full.txt)</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the llms.txt content as string
    /// </returns>
    Task<string> PrepareLlmsTextFileAsync(bool full = false);
}
