using Bogus;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

public class UserSeeder : IHostedService
{
    private readonly IUserService _userService;
    private readonly IRuntimeState _runtimeState;
    private readonly SeederConfiguration _config;

    public UserSeeder(
        IUserService userService,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config)
    {
        _userService = userService;
        _runtimeState = runtimeState;
        _config = config.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Only seed when Umbraco is fully installed and running
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            Console.WriteLine("UserSeeder: Skipping - Umbraco is not fully installed yet.");
            return Task.CompletedTask;
        }

        SeedUsers();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedUsers()
    {
        var targetCount = _config.Users.Count;

        // Skip if we already have seeded users (check for TestUser_ prefix)
        var existingUsers = _userService.GetAll(0, 100, out _);
        if (existingUsers.Any(u => u.Username.StartsWith("TestUser_"))) return;

        var faker = new Faker("en");

        // Get user groups to assign to users
        var adminGroup = _userService.GetUserGroupByAlias("admin");
        var editorGroup = _userService.GetUserGroupByAlias("editor");
        var writerGroup = _userService.GetUserGroupByAlias("writer");
        var sensitiveDataGroup = _userService.GetUserGroupByAlias("sensitiveData");
        var translatorsGroup = _userService.GetUserGroupByAlias("translator");

        // Calculate distribution (20% each group)
        int groupSize = targetCount / 5;

        int created = 0;

        for (int i = 1; i <= targetCount; i++)
        {
            var firstName = faker.Name.FirstName();
            var lastName = faker.Name.LastName();
            var username = $"TestUser_{i}";
            var email = $"testuser{i}@example.com";

            // Check if user already exists
            var existing = _userService.GetByUsername(username);
            if (existing != null) continue;

            var user = _userService.CreateUserWithIdentity(username, email);

            user.Name = $"{firstName} {lastName}";

            // Assign user groups based on index for variety (distribute evenly)
            if (i <= groupSize && adminGroup != null)
            {
                user.AddGroup(adminGroup.ToReadOnlyGroup());
            }
            else if (i <= groupSize * 2 && editorGroup != null)
            {
                user.AddGroup(editorGroup.ToReadOnlyGroup());
            }
            else if (i <= groupSize * 3 && writerGroup != null)
            {
                user.AddGroup(writerGroup.ToReadOnlyGroup());
            }
            else if (i <= groupSize * 4 && sensitiveDataGroup != null)
            {
                user.AddGroup(sensitiveDataGroup.ToReadOnlyGroup());
            }
            else if (translatorsGroup != null)
            {
                user.AddGroup(translatorsGroup.ToReadOnlyGroup());
            }

            _userService.Save(user);
            created++;
        }

        Console.WriteLine($"Seeded {created} test users (target: {targetCount}).");
    }
}
