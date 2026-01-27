namespace Umbraco.Community.PerformanceTestDataSeeder.Seeders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;
using Umbraco.Community.PerformanceTestDataSeeder.Configuration;
using Umbraco.Community.PerformanceTestDataSeeder.Infrastructure;

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
        IScopeProvider scopeProvider,
        ILogger<UserSeeder> logger,
        IRuntimeState runtimeState,
        IOptions<SeederConfiguration> config,
        IOptions<SeederOptions> options,
        SeederExecutionContext context)
        : base(logger, runtimeState, config, options, context, scopeProvider)
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
        var prefix = GetPrefix(PrefixType.User);
        var users = _userService.GetAll(0, 100, out _);
        return users.Any(u => u.Username.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    protected override Task SeedAsync(CancellationToken cancellationToken)
    {
        var targetCount = Config.Users.Count;
        var prefix = GetPrefix(PrefixType.User);

        if (IsDryRun)
        {
            Logger.LogInformation("[DRY-RUN] Would create {Count} users with prefix '{Prefix}'", targetCount, prefix);
            return Task.CompletedTask;
        }

        // Get all user groups in one call (instead of 5 separate calls)
        var allGroups = _userService.GetAllUserGroups().ToDictionary(g => g.Alias, StringComparer.OrdinalIgnoreCase);
        allGroups.TryGetValue("admin", out var adminGroup);
        allGroups.TryGetValue("editor", out var editorGroup);
        allGroups.TryGetValue("writer", out var writerGroup);
        allGroups.TryGetValue("sensitiveData", out var sensitiveDataGroup);
        allGroups.TryGetValue("translator", out var translatorGroup);

        // Collect available groups for fallback assignment
        var availableGroups = new[] { adminGroup, editorGroup, writerGroup, sensitiveDataGroup, translatorGroup }
            .Where(g => g != null)
            .ToList();

        if (availableGroups.Count == 0)
        {
            Logger.LogWarning("No user groups found! Users will be created without group assignments.");
        }
        else
        {
            Logger.LogDebug("Found {Count} user groups for assignment", availableGroups.Count);
        }

        // Pre-load existing usernames using pagination to avoid memory issues on large databases
        var existingUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const int pageSize = 1000;
        long totalUsers;
        int pageIndex = 0;

        do
        {
            var usersPage = _userService.GetAll(pageIndex, pageSize, out totalUsers);
            foreach (var user in usersPage)
            {
                existingUsernames.Add(user.Username);
            }
            pageIndex++;
        } while (pageIndex * pageSize < totalUsers);

        Logger.LogDebug("Found {Count} existing users", existingUsernames.Count);

        // Calculate distribution (20% each group)
        int groupSize = targetCount / 5;
        int created = 0;
        int usersWithoutGroups = 0;
        int batchCount = 0;
        IScope? currentScope = null;

        try
        {
            for (int i = 1; i <= targetCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var username = $"{prefix}{i}";

                // Skip if user already exists (O(1) HashSet lookup instead of DB query)
                if (existingUsernames.Contains(username))
                {
                    Logger.LogDebug("User {Username} already exists, skipping", username);
                    continue;
                }

                try
                {
                    // Start new batch scope if needed
                    if (currentScope == null)
                    {
                        currentScope = CreateScopedBatch();
                        batchCount = 0;
                    }

                    var firstName = Context.Faker.Name.FirstName();
                    var lastName = Context.Faker.Name.LastName();
                    var email = $"{username.ToLowerInvariant()}@example.com";

                    var user = _userService.CreateUserWithIdentity(username, email);
                    user.Name = $"{firstName} {lastName}";

                    // Assign user groups based on index for variety
                    bool groupAssigned = false;
                    if (i <= groupSize && adminGroup != null)
                    {
                        user.AddGroup(adminGroup.ToReadOnlyGroup());
                        groupAssigned = true;
                    }
                    else if (i <= groupSize * 2 && editorGroup != null)
                    {
                        user.AddGroup(editorGroup.ToReadOnlyGroup());
                        groupAssigned = true;
                    }
                    else if (i <= groupSize * 3 && writerGroup != null)
                    {
                        user.AddGroup(writerGroup.ToReadOnlyGroup());
                        groupAssigned = true;
                    }
                    else if (i <= groupSize * 4 && sensitiveDataGroup != null)
                    {
                        user.AddGroup(sensitiveDataGroup.ToReadOnlyGroup());
                        groupAssigned = true;
                    }
                    else if (translatorGroup != null)
                    {
                        user.AddGroup(translatorGroup.ToReadOnlyGroup());
                        groupAssigned = true;
                    }

                    // Fallback: assign to first available group if no group was assigned
                    if (!groupAssigned && availableGroups.Count > 0)
                    {
                        var fallbackGroup = availableGroups[i % availableGroups.Count];
                        user.AddGroup(fallbackGroup!.ToReadOnlyGroup());
                        groupAssigned = true;
                    }

                    if (!groupAssigned)
                    {
                        usersWithoutGroups++;
                    }

                    _userService.Save(user);
                    created++;
                    batchCount++;

                    // Complete batch when size reached
                    if (batchCount >= Options.BatchSize)
                    {
                        currentScope.Complete();
                        currentScope.Dispose();
                        currentScope = null;
                    }

                    LogProgress(created, targetCount, "users");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to create user {Username}", username);
                    if (Options.StopOnError) throw;
                }
            }

            // Complete any remaining batch
            if (currentScope != null)
            {
                currentScope.Complete();
            }
        }
        finally
        {
            currentScope?.Dispose();
        }

        if (usersWithoutGroups > 0)
        {
            Logger.LogWarning("Created {Count} users without group assignments (no groups available)", usersWithoutGroups);
        }

        Logger.LogInformation("Seeded {Created} test users (target: {Target})", created, targetCount);

        return Task.CompletedTask;
    }
}
