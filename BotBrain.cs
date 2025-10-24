using Bot.Control;
using Bot.Navigation;
using Bot.Vision;
using OpenCvSharp;

namespace Bot
{
    public sealed class BotBrain
    {
        private readonly MapRepository _maps = new();
        private readonly MinimapLocalizer _loc = new();
        private readonly MinimapAnalyzer _minimap = new();
        private readonly AStar _astar = new();
        private readonly KeyMover _mover = new();

        private PlayerPosition _playerPosition;

        private readonly List<(int x, int y, int z)> _waypoints = new();

        private bool _recordMode = false;
        private bool _running = false;
        private int _wpIndex = 0;

        public BotBrain()
        {
            _maps.LoadAll("Assets/Minimaps"); // folder with stitched PNG maps
        }

        // --- Control commands ---

        public void ToggleRecord() => _recordMode = !_recordMode;

        public void AddWaypoint()
        {
            if (_playerPosition.IsValid is false)
            {
                Console.WriteLine("[Bot] ⚠️ Cannot add waypoint – player position unknown.");
                return;
            }

            _waypoints.Add((_playerPosition.X, _playerPosition.Y, _playerPosition.Floor));
            Console.WriteLine($"[Bot] Added waypoint #{_waypoints.Count} at (x={_playerPosition.X}) (y={_playerPosition.Y}) (z={_playerPosition.Floor}).");
        }

        public void StartBot()
        {
            if (_waypoints.Count == 0)
            {
                Console.WriteLine("[Bot] ⚠️ No waypoints set.");
                return;
            }

            _wpIndex = 0;
            _running = true;
            Console.WriteLine("[Bot] 🤖 Bot started.");
        }

        public void StopBot()
        {
            _running = false;
            Console.WriteLine("[Bot] ⛔ Bot stopped.");
        }

        // --- Main processing loop ---
        public void ProcessFrame(Mat frame)
        {
            // Extract minimap region from current frame
            using var mini = _minimap.ExtractMinimap(frame);
            if (mini.Empty())
                return;

            // Localize player position
            var playerPosition = _loc.Locate(mini, _maps);
            if (playerPosition.Confidence < 0.3) return; // ignore uncertain detections
            _playerPosition = playerPosition;


            if (_recordMode)
                Console.WriteLine($"[REC] ({playerPosition.X},{playerPosition.Y}) z={playerPosition.Floor}");

            if (_running)
                StepBot((playerPosition.X, playerPosition.Y), _maps.Get(playerPosition.Floor));
        }

        // --- Navigation / movement ---
        private void StepBot((int x, int y) player, FloorData floor)
        {
            if (_wpIndex >= _waypoints.Count)
            {
                Console.WriteLine("[Bot] ✅ Finished all waypoints.");
                _running = false;
                return;
            }

            var target = _waypoints[_wpIndex];
            if(player == (target.x, target.y))
            {
                _wpIndex++;
                Console.WriteLine($"[Bot] Reached waypoint #{_wpIndex} ({target.x},{target.y},{target.z})");
                return;
            }


            var path = _astar.FindPath(floor.Walkable, player, (target.x, target.y));
            if (path.Count < 2)
                return;

            _mover.StepTowards(player, path[1]);

            if (player == (target.x, target.y))
            {
                _wpIndex++;
                Console.WriteLine($"[Bot] Reached waypoint #{_wpIndex} ({target.x},{target.y},{target.z})");
            }
        }
    }
}