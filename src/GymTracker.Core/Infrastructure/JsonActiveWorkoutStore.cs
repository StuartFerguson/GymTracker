using System.Text.Json;
using GymTracker.Application;

namespace GymTracker.Core.Infrastructure;

public sealed class JsonActiveWorkoutStore : IActiveWorkoutStore
{
    private readonly string filePath;
    private readonly string temporaryFilePath;
    private readonly JsonSerializerOptions options;

    public JsonActiveWorkoutStore(string filePath, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
        temporaryFilePath = filePath + ".tmp";
        this.options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.General);
    }

    public async Task SaveAsync(ActiveWorkoutSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var stream = new FileStream(
            temporaryFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(true);
        }

        File.Move(temporaryFilePath, filePath, true);
    }

    public async Task<ActiveWorkoutSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<ActiveWorkoutSnapshot>(stream, options, cancellationToken);
        }
        catch (JsonException)
        {
            DeleteInvalidSnapshot();
            return null;
        }
        catch (IOException)
        {
            DeleteInvalidSnapshot();
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            DeleteInvalidSnapshot();
            return null;
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private void DeleteInvalidSnapshot()
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
