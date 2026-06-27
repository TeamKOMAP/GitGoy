namespace Vcs.Desktop.Services;

public static class RepositoryDataServiceFactory
{
    public static async Task<IRepositoryDataService> SignInAsync(string username, string password)
    {
        var options = ApiClientOptions.Load() with
        {
            Username = string.IsNullOrWhiteSpace(username) ? "desktop-client" : username.Trim(),
            Password = password
        };

        return await ApiRepositoryDataService.CreateAsync(options);
    }
}
