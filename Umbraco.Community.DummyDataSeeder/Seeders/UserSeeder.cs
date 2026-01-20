namespace Umbraco.Community.DummyDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using Umbraco.Community.DummyDataSeeder.Configuration;
using Umbraco.Community.DummyDataSeeder.Infrastructure;

/// <summary>
/// Seeds test users distributed across user groups.
/// Execution order: 7 (last seeder, no dependencies).
/// </summary>
public class UserSeeder : BaseSeeder<UserSeeder>
{
    private readonly IUserService _userService;

    /// <summary>
    /// Creates a new UserSeeder instance.
    /// </summary>
    public UserSeeder(
        IUserService userService,
        ILogger<UserSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context)
    {
        _userService = userService;
    }

    /// <inheritdoc />
    public override int ExecutionOrder => 7;

    /// <inheritdoc />
    public override string SeederName => "UserSeeder";

    /// <inheritdoc />
    protected override bool ShouldExecute() => Options.EnabledSeeders.Users;

    /// <inheritdoc />
    protected override bool IsAlreadySeeded()
    {
        var prefix = GetPrefix("user");
        var users = _userService.GetAll(0, 100, out _);
        return users.Any(u => u.Username.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        var targetCount = Config.Users.Count;
        var prefix = GetPrefix("user");

        // Get user groups
        var adminGroup = _userService.GetUserGroupByAlias("admin");
        var editorGroup = _userService.GetUserGroupByAlias("editor");
        var writerGroup = _userService.GetUserGroupByAlias("writer");
        var sensitiveDataGroup = _userService.GetUserGroupByAlias("sensitiveData");
        var translatorGroup = _userService.GetUserGroupByAlias("translator");

        // Calculate distribution (20% each group)
        int groupSize = targetCount / 5;
        int created = 0;

        for (int i = 1; i <= targetCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var username = $"{prefix}{i}";

            // Skip if user already exists
            var existing = _userService.GetByUsername(username);
            if (existing != null)
            {
                Logger.LogDebug("User {Username} already exists, skipping", username);
                continue;
            }

            try
            {
                var firstName = Context.Faker.Name.FirstName();
                var lastName = Context.Faker.Name.LastName();
                var email = $"{username.ToLowerInvariant()}@example.com";

                var user = _userService.CreateUserWithIdentity(username, email);
                user.Name = $"{firstName} {lastName}";

                // Assign user groups based on index for variety
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
                else if (translatorGroup != null)
                {
                    user.AddGroup(translatorGroup.ToReadOnlyGroup());
                }

                _userService.Save(user);
                created++;

                LogProgress(created, targetCount, "users");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create user {Username}", username);
                if (Options.StopOnError) throw;
            }
        }

        Logger.LogInformation("Seeded {Created} test users (target: {Target})", created, targetCount);

        return Task.CompletedTask;
    }
}
