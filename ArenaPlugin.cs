using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Microsoft.Xna.Framework;

namespace ArenaPlugin
{
    [ApiVersion(2, 1)]
    public class ArenaPlugin : TerrariaPlugin
    {
        public override string Name => "SimpleArenaReborn";
        public override string Description => "Мини-игра: Выживание с ручной остановкой";
        public override Version Version => new Version(1, 4);
        public override string Author => "yomissayy";

        // Флаг для контроля работы арены
        private bool _isArenaRunning = false;

        public ArenaPlugin(Main game) : base(game) { }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("arena.admin", ArenaCommand, "arenastart"));
        }

        private async void ArenaCommand(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Использование: /arenastart [название региона] [кол-во волн] ИЛИ /arenastart stop");
                return;
            }

            // Обработка команды STOP
            if (args.Parameters[0].ToLower() == "stop")
            {
                if (!_isArenaRunning)
                {
                    args.Player.SendErrorMessage("Арена сейчас не запущена.");
                    return;
                }

                _isArenaRunning = false;
                TSPlayer.All.SendMessage("[ARENA] Игра принудительно остановлена администратором!", Color.Pink);
                return;
            }

            // Обработка запуска (если параметров 2)
            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Использование: /arenastart [название региона] [кол-во волн]");
                return;
            }

            if (_isArenaRunning)
            {
                args.Player.SendErrorMessage("Арена уже запущена! Остановите её командой /arenastart stop.");
                return;
            }

            string regionName = args.Parameters[0];
            if (!int.TryParse(args.Parameters[1], out int totalWaves) || totalWaves <= 0)
            {
                args.Player.SendErrorMessage("Некорректное количество волн.");
                return;
            }

            Region? region = TShock.Regions.GetRegionByName(regionName);
            if (region == null)
            {
                args.Player.SendErrorMessage($"Регион '{regionName}' не найден!");
                return;
            }

            _isArenaRunning = true;
            await Task.Run(() => GameLoop(region, totalWaves));
        }

        private async Task GameLoop(Region region, int totalWaves)
        {
            for (int wave = 1; wave <= totalWaves; wave++)
            {
                // Проверка флага остановки и наличия игроков
                if (!_isArenaRunning || !IsPlayersInRegion(region))
                {
                    StopGame();
                    return;
                }

                TSPlayer.All.SendMessage($"[ARENA] Волна {wave}/{totalWaves} начнется через 5 секунд!", Color.Yellow);
                
                // Проверка остановки во время ожидания (5 секунд)
                for (int i = 0; i < 5; i++)
                {
                    if (!_isArenaRunning) return;
                    await Task.Delay(1000);
                }

                int mobsToSpawn = 5 + (wave * 3);
                int[] waveMobIds = GetMobListForWave(wave);
                SpawnMobs(region, mobsToSpawn, waveMobIds);

                TSPlayer.All.SendMessage($"[ARENA] Волна {wave} пошла!", Color.Red);

                while (IsMobsInRegion(region))
                {
                    // Проверка остановки или отсутствия игроков во время боя
                    if (!_isArenaRunning || !IsPlayersInRegion(region))
                    {
                        StopGame();
                        return;
                    }
                    await Task.Delay(2000);
                }

                TSPlayer.All.SendMessage($"[ARENA] Волна {wave} зачищена!", Color.LightGreen);
                
                if (wave < totalWaves)
                {
                    await Task.Delay(8000);
                }
            }

            TSPlayer.All.SendMessage("[ARENA] ПОБЕДА! Все испытания пройдены!", Color.Gold);
            _isArenaRunning = false;
        }

        private void StopGame()
        {
            if (_isArenaRunning) // Если остановилось само (из-за смерти игроков)
            {
                TSPlayer.All.SendMessage("[ARENA] Игра окончена.", Color.Pink);
            }
            _isArenaRunning = false;
        }

        private int[] GetMobListForWave(int wave)
        {
            if (wave <= 2) return new int[] { 3, 430, 21 };
            if (wave <= 5) return new int[] { 21, 201, 77, 110 };
            if (wave <= 8) return new int[] { 269, 291, 292, 471 };
            return new int[] { 325, 344, 345 };
        }

        private void SpawnMobs(Region region, int count, int[] mobIds)
        {
            Random rnd = new Random();
            int xMin = (int)region.Area.Left;
            int xMax = (int)region.Area.Right;
            int yMin = (int)region.Area.Top;
            int yMax = (int)region.Area.Bottom;

            int spawned = 0;
            while (spawned < count && _isArenaRunning) // Добавлена проверка флага при спавне
            {
                int sX = rnd.Next(xMin, xMax);
                int sY = rnd.Next(yMin, yMax);
                int finalX = (int)MathHelper.Clamp(sX, 0, Main.maxTilesX);
                int finalY = (int)MathHelper.Clamp(sY, 0, Main.maxTilesY);

                int npcIndex = NPC.NewNPC(null, finalX * 16, finalY * 16, mobIds[rnd.Next(mobIds.Length)]);
                if (npcIndex < 200)
                {
                    Main.npc[npcIndex].TargetClosest();
                    spawned++;
                }
                Thread.Sleep(200);
            }
        }

        private bool IsMobsInRegion(Region region)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc != null && npc.active && !npc.friendly && npc.lifeMax > 10)
                {
                    if (region.Area.Contains((int)(npc.position.X / 16), (int)(npc.position.Y / 16))) return true;
                }
            }
            return false;
        }

        private bool IsPlayersInRegion(Region region)
        {
            return TShock.Players.Any(p => p != null && p.Active && !p.Dead && region.Area.Contains(p.TileX, p.TileY));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _isArenaRunning = false;
            base.Dispose(disposing);
        }
    }
}
