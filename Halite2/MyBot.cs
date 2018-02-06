using System;
using Halite2.hlt;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Halite2
{
    public class MyBot
    {

        public static void Main(string[] args)
        {
            string name = args.Length > 0 ? args[0] : "Galcon";

            Networking networking = new Networking();
            GameMap gameMap = networking.Initialize(name);

            List<Move> moveList = new List<Move>();

            for (; ; )
            {
                moveList.Clear();
                gameMap.UpdateMap(Networking.ReadLineIntoMetadata());

                var ships = gameMap.GetMyPlayer().GetShips().Values;

                InitTurn(gameMap, moveList, ships);
                Networking.SendMoves(moveList);
            }
        }

        private static void InitTurn(GameMap gameMap, List<Move> moveList, ICollection<Ship> ships)
        {
            int current = 0;
            int totalCurrent = 0;
            var shipCount = gameMap.GetMyPlayer().GetShips().Values.Count;
            var useableShips = ships.Where(s => s.GetDockingStatus() == Ship.DockingStatus.Undocked).ToList();
            var useableCount = useableShips.Count;
            var enemyPlanets = gameMap.GetAllPlanets().Values
                .Where(p => p.IsOwned() && p.GetOwner() != gameMap.GetMyPlayerId()).ToList();
            var myPlanets = gameMap.GetAllPlanets().Values
                .Where(p => p.IsOwned() && p.GetOwner() == gameMap.GetMyPlayerId()).ToList();


            if (enemyPlanets.Count <= 0 && myPlanets.Count >= 5)
            {
                DebugLog.AddLog("No enemy planets - Now Attack");
                Parallel.ForEach(useableShips, ship =>
                {
                    bool moved = false;
                    var sh = gameMap.GetAllShips().FindNearestEnemyShip(ship);
                    ThrustMove newThrustMove = Navigation.NavigateShipToDock(gameMap, ship, sh, Constants.MAX_SPEED);
                    if (newThrustMove != null)
                    {
                        moved = true;
                        moveList.Add(newThrustMove);

                    }

                    if (!moved)
                    {
                        var planets = gameMap.GetAllPlanets().Values.OrderBy(ship.GetDistanceTo).ToList();
                        foreach (var planet in planets)
                        {
                            if (ship.CanDock(planet))
                            {
                                moved = true;
                                moveList.Add(new DockMove(ship, planet));
                                break;
                            }
                        }
                    }
                });
                return;
            }

            Parallel.ForEach(useableShips, ship =>
            {
                totalCurrent++;
                bool moved = false;
                if (ship.GetDockingStatus() != Ship.DockingStatus.Undocked)
                {
                    return;
                }
                current++;

                List<Planet> planets;

                try
                {
                    planets = gameMap.GetAllPlanets().Values.OrderBy(ship.GetDistanceTo).ThenByDescending(p => p.GetRadius()).ToList();
                }
                catch (Exception e)
                {
                    planets = gameMap.GetAllPlanets().Values.ToList();
                }

                if (current == 1 || (useableCount > 20 && current > useableCount - 4))
                {
                    if (gameMap.GetAllPlayers().Count == 2 && shipCount > 1)
                    {
                        goto ATTACK_ENEMY;
                    }
                    var nPs = gameMap.GetAllPlanets().Values.Where(p => p.GetDistanceTo(ship) <= planets[4].GetDistanceTo(ship)).OrderByDescending(p => p.GetRadius()).ToList();
                    if (nPs.Count > 0)
                    {
                        planets = nPs;
                    }
                }

                foreach (Planet planet in planets)
                {
                    if (planet.IsOwned())
                    {
                        if ( //current > 15 && (current > useableCount / 4 * 5)|| 
                            useableCount >= 4 ||
                            planet.GetRadius() >= 8)
                            
                        {
                            if (planet.GetOwner() == gameMap.GetMyPlayerId() && !planet.DeservesMine())
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (ship.CanDock(planet))
                    {
                        if (!planet.IsOwned() || planet.GetOwner() == gameMap.GetMyPlayerId() || planet.GetDockedShips().Count <= 0)
                        {
                            if (!planet.IsFull())
                            {
                                moved = true;
                                moveList.Add(new DockMove(ship, planet));
                                break;
                            }
                        }

                        //var eShip = gameMap.GetShip(planet.GetOwner(),
                        //    planet.GetDockedShips()[planet.GetDockedShips().RandomPick()]);
                        var eShip = gameMap.GetAllShips().FindNearestEnemyShip(ship);
                        if (eShip != null)
                        {
                            ThrustMove attackMove = Navigation.NavigateShipToDock(gameMap, ship, eShip, Constants.MAX_SPEED);
                            if (attackMove != null)
                            {
                                moved = true;
                                moveList.Add(attackMove);
                                break;
                            }
                        }
                    }

                    var speed = myPlanets.Count > 0 ? Constants.MAX_SPEED : Helper.Random.Next(5, 8);
                    ThrustMove newThrustMove = Navigation.NavigateShipToDock(gameMap, ship, planet, speed);
                    if (newThrustMove != null)
                    {
                        moved = true;
                        moveList.Add(newThrustMove);
                    }
                    break;
                }
ATTACK_ENEMY:
                if (!moved)
                {
                    DebugLog.AddLog("Not moved - Now Attacking Enemy");
                    //foreach (var sh in gameMap.GetAllShips().Where(s => s.GetOwner() != gameMap.GetMyPlayerId()).Take(20).OrderBy(s => s.GetDistanceTo(ship)))
                    {
                        var sh = gameMap.GetAllShips().FindNearestEnemyShip(ship);
                        ThrustMove newThrustMove = Navigation.NavigateShipToDock(gameMap, ship, sh, Constants.MAX_SPEED);
                        if (newThrustMove != null)
                        {
                            moved = true;
                            moveList.Add(newThrustMove);
                            //break;
                        }
                    }
                }

                if (!moved)
                {
                    DebugLog.AddLog("Not moved - Now Dock Self");
                    foreach (Planet planet in planets)
                    {
                        if (ship.CanDock(planet))
                        {
                            moved = true;
                            moveList.Add(new DockMove(ship, planet));
                            break;
                        }

                        ThrustMove newThrustMove = Navigation.NavigateShipToDock(gameMap, ship, planet, Constants.MAX_SPEED);
                        if (newThrustMove != null)
                        {
                            moved = true;
                            moveList.Add(newThrustMove);
                        }
                        break;
                    }
                }

                if (!moved)
                {
                    DebugLog.AddLog("Not moved - Now Random");
                    foreach (Planet planet in planets)
                    {
                        if (planet.IsOwned())
                        {
                            continue;
                        }

                        moved = true;
                        moveList.Add(new DockMove(ship, planet));
                        break;
                    }
                }
            });

        }
    }

    public static class Helper
    {
        public static Ship FindNearestEnemyShip(this ICollection<Ship> ships, Ship ship)
        {
            double min = double.MaxValue;
            Ship minShip = null;
            foreach (var s in ships)
            {
                if (s.GetOwner() == ship.GetOwner())
                {
                    continue;
                }
                var cost = s.GetDistanceTo(ship);
                if (cost < min)
                {
                    min = cost;
                    minShip = s;
                }
            }
            return minShip;
        }

        public static Random Random = new Random();

        public static T RandomPick<T>(this IList<T> list)
        {
            return list[Random.Next(0, list.Count)];
        }

        public static bool DeservesMine(this Planet planet)
        {
            if (planet.IsFull())
            {
                return false;
            }

            //if (planet.GetRadius() > 10)
            //{
            //    return true;
            //}

            //if (planet.GetRadius() > 6 && planet.GetDockingSpots() - planet.GetDockedShips().Count > 1)
            //{
            //    return true;
            //}

            //if (planet.GetRadius() > 5 && planet.GetDockingSpots() - planet.GetDockedShips().Count > 1)
            //{
            //    return true;
            //}

            //return false;

            return true;
        }
    }
}
