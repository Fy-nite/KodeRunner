using System.Collections.Concurrent;
using System.Net.WebSockets;
using Newtonsoft.Json;

namespace KodeRunner.Collaboration
{
    public class CollaborationManager
    {
        private readonly ConcurrentDictionary<string, CollaborationSession> _sessions = new();
        private readonly ConcurrentDictionary<string, List<string>> _sessionParticipants = new();

        public string CreateSession(string projectName, string hostConnectionId)
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new CollaborationSession
            {
                SessionId = sessionId,
                ProjectName = projectName,
                HostConnectionId = hostConnectionId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            
            _sessions[sessionId] = session;
            _sessionParticipants[sessionId] = new List<string> { hostConnectionId };
            
            Logger.Log($"Collaboration session created: {sessionId} for project: {projectName}");
            return sessionId;
        }

        public async Task BroadcastFileChange(string sessionId, FileChangeEvent changeEvent)
        {
            if (!_sessions.TryGetValue(sessionId, out var session) || !session.IsActive)
                return;

            var participants = _sessionParticipants.GetValueOrDefault(sessionId, new List<string>());
            var message = JsonConvert.SerializeObject(new
            {
                type = "file_change",
                sessionId,
                change = changeEvent
            });

            foreach (var participantId in participants.Where(p => p != changeEvent.AuthorId))
            {
                await Program.connectionManager.SendToConnection(participantId, message);
            }
        }

        public void AddParticipant(string sessionId, string connectionId)
        {
            if (_sessionParticipants.TryGetValue(sessionId, out var participants))
            {
                participants.Add(connectionId);
                Logger.Log($"User {connectionId} joined collaboration session {sessionId}");
            }
        }
    }

    public class CollaborationSession
    {
        public string SessionId { get; set; }
        public string ProjectName { get; set; }
        public string HostConnectionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class FileChangeEvent
    {
        public string FileName { get; set; }
        public string Content { get; set; }
        public string AuthorId { get; set; }
        public DateTime Timestamp { get; set; }
        public ChangeType Type { get; set; }
    }

    public enum ChangeType
    {
        Create,
        Update,
        Delete,
        Rename
    }
}
