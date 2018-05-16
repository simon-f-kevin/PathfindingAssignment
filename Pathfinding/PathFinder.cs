#region Using Statements
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
#endregion

namespace Pathfinding
{
    #region Search Status Enum
    public enum SearchStatusEnum
    {
        Stopped,
        Searching,
        NoPath,
        PathFound,
    }
    #endregion

    #region Search Method Enum
    public enum SearchMethodEnum
    {
        BreadthFirst,
        BestFirst,
        AStar,
        DepthFirst,
        Max,
    }
    #endregion

    class PathFinder
    {
        #region Search Node Struct
        /// <summary>
        /// Reresents one node in the search space
        /// </summary>
        private struct SearchNode
        {
            /// <summary>
            /// Location on the map
            /// </summary>
            public Point Position;

            /// <summary>
            /// Distance to goal estimate
            /// </summary>
            public int DistanceToGoal;

            /// <summary>
            /// Distance traveled from the start
            /// </summary>
            public int DistanceTraveled;

            public SearchNode(
                Point mapPosition, int distanceToGoal, int distanceTraveled)
            {
                Position = mapPosition;
                DistanceToGoal = distanceToGoal;
                DistanceTraveled = distanceTraveled;
            }
        }
        #endregion

        #region Constants        

        /// <summary>
        /// Scales the draw size of the search nodes
        /// </summary>
        const float searchNodeDrawScale = .75f;

        #endregion

        #region Fields

        //Draw data
        private Texture2D nodeTexture;
        private Vector2 nodeTextureCenter;
        private Color openColor = Color.Green;
        private Color closedColor = Color.Red;

        // How much time has passed since the last search step
        private float timeSinceLastSearchStep = 0f;
        // Holds search nodes that are avaliable to search
        private List<SearchNode> openList;
        // Holds the nodes that have already been searched
        private List<SearchNode> closedList;
        // Holds all the paths we've creted so far
        private Dictionary<Point, Point> paths;
        // The map we're searching
        private Map map;
        // Seconds per search step        
        public float timeStep = .5f;

        //depth first
        private SearchNode lastVisitedNode;
        private Stack<SearchNode> openStack;
        private Stack<SearchNode> closedStack;
        private Stack<SearchNode> visitedNodesStack;
        private Point startPosition;
        

        #endregion

        #region Properties

        // Tells us if the search is stopped, started, finished or failed
        public SearchStatusEnum SearchStatus
        {
            get { return searchStatus; }
        }
        private SearchStatusEnum searchStatus;

        // Tells us which search type we're using right now
        public SearchMethodEnum SearchMethod
        {
            get { return searchMethod; }
        }
        private SearchMethodEnum searchMethod = SearchMethodEnum.BestFirst;

        public float Scale
        {
            get { return scale; }
            set { scale = value * searchNodeDrawScale; }
        }
        private float scale;

        // Seconds per search step
        public float TimeStep
        {
            get { return timeStep; }
            set { timeStep = value; }
        }

        /// <summary>
        /// Toggles searching on and off
        /// </summary>
        public bool IsSearching
        {
            get { return searchStatus == SearchStatusEnum.Searching; }
            set
            {
                if (searchStatus == SearchStatusEnum.Searching)
                {
                    searchStatus = SearchStatusEnum.Stopped;
                }
                else if (searchStatus == SearchStatusEnum.Stopped)
                {
                    searchStatus = SearchStatusEnum.Searching;
                }
            }
        }

        /// <summary>
        /// How many search steps have elapsed on this map
        /// </summary>
        public int TotalSearchSteps
        {
            get { return totalSearchSteps; }
        }
        private int totalSearchSteps = 0;

        #endregion

        #region Initialization

        /// <summary>
        /// Setup search
        /// </summary>
        /// <param name="mazeMap">Map to search</param>
        public void Initialize(Map mazeMap)
        {
            searchStatus = SearchStatusEnum.Stopped;
            openList = new List<SearchNode>();
            closedList = new List<SearchNode>();
            paths = new Dictionary<Point, Point>();
            map = mazeMap;

            //for depth first
            visitedNodesStack = new Stack<SearchNode>();
        }

        /// <summary>
        /// Load the Draw texture
        /// </summary>
        public void LoadContent(ContentManager content)
        {
            nodeTexture = content.Load<Texture2D>("dot");
            nodeTextureCenter =
                new Vector2(nodeTexture.Width / 2, nodeTexture.Height / 2);
        }

        #endregion

        #region Update and Draw

        /// <summary>
        /// Search Update
        /// </summary>
        public void Update(GameTime gameTime)
        {
            if (searchStatus == SearchStatusEnum.Searching)
            {
                timeSinceLastSearchStep += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (timeSinceLastSearchStep >= timeStep)
                {
                    DoSearchStep();
                    timeSinceLastSearchStep = 0f;
                }
            }
        }

        /// <summary>
        /// Draw the search space
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            if (searchStatus != SearchStatusEnum.PathFound)
            {
                spriteBatch.Begin();
                foreach (SearchNode node in openList)
                {
                    spriteBatch.Draw(nodeTexture,
                        map.MapToWorld(node.Position, true), null, openColor, 0f,
                        nodeTextureCenter, scale, SpriteEffects.None, 0f);
                }
                foreach (SearchNode node in closedList)
                {
                    spriteBatch.Draw(nodeTexture,
                        map.MapToWorld(node.Position, true), null, closedColor, 0f,
                        nodeTextureCenter, scale, SpriteEffects.None, 0f);
                }
                spriteBatch.End();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Reset the search
        /// </summary>
        public void Reset()
        {
            searchStatus = SearchStatusEnum.Stopped;
            totalSearchSteps = 0;
            Scale = map.Scale;
            openList.Clear();
            closedList.Clear();
            paths.Clear();
            openList.Add(new SearchNode(map.StartTile,
                Map.StepDistance(map.StartTile, map.EndTile)
                , 0));
            startPosition = map.StartTile;

            visitedNodesStack.Clear();
            //openStack.Clear();
            //closedStack.Clear();
        }

        /// <summary>
        /// Cycle through the search method to the next type
        /// </summary>
        public void NextSearchType()
        {
            searchMethod = (SearchMethodEnum)(((int)searchMethod + 1) %
                (int)SearchMethodEnum.Max);
        }

        /// <summary>
        /// This method find the next path node to visit, puts that node on the 
        /// closed list and adds any nodes adjacent to the visited node to the 
        /// open list.
        /// </summary>
        private void DoSearchStep()
        {
            SearchNode newOpenListNode;

            bool foundNewNode = SelectNodeToVisit(out newOpenListNode);
            if (foundNewNode)
            {
                Point currentPos = newOpenListNode.Position;
                foreach (Point point in map.OpenMapTiles(currentPos))
                {
                    SearchNode mapTile = new SearchNode(point,
                        map.StepDistanceToEnd(point),
                        newOpenListNode.DistanceTraveled + 1);
                    if (!InList(openList, point) &&
                        !InList(closedList, point))
                    {
                        openList.Add(mapTile);
                        paths[point] = newOpenListNode.Position;
                    }
                }
                if (currentPos == map.EndTile)
                {
                    searchStatus = SearchStatusEnum.PathFound;
                }
                openList.Remove(newOpenListNode);
                closedList.Add(newOpenListNode);
            }
            else
            {
                searchStatus = SearchStatusEnum.NoPath;
            }
        }

        /// <summary>
        /// Determines if the given Point is inside the SearchNode list given
        /// </summary>
        private static bool InList(List<SearchNode> list, Point point)
        {
            bool inList = false;
            foreach (SearchNode node in list)
            {
                if (node.Position == point)
                {
                    inList = true;
                }
            }
            return inList;
        }

        /// <summary>
        /// This Method looks at everything in the open list and chooses the next 
        /// path to visit based on which search type is currently selected.
        /// </summary>
        /// <param name="result">The node to be visited</param>
        /// <returns>Whether or not SelectNodeToVisit found a node to examine
        /// </returns>
        private bool SelectNodeToVisit(out SearchNode result)
        {
            result = new SearchNode();
            bool success = false;
            float smallestDistance = float.PositiveInfinity;
            float currentDistance = 0f;
            if (openList.Count > 0)
            {
                switch (searchMethod)
                {
                    // Breadth first search looks at every possible path in the 
                    // order that we see them in.
                    case SearchMethodEnum.BreadthFirst:
                        totalSearchSteps++;
                        result = openList[0];
                        success = true;
                        break;
                    //Depth first search traverses the search tree until it finds a leaf node
                    //It wants to go move as far away from the start as quick as possible
                    //If the leafnode is the goal it terminates
                    case SearchMethodEnum.DepthFirst:
                        totalSearchSteps++;
                        openStack = new Stack<SearchNode>(openList);
                        closedStack = new Stack<SearchNode>(closedList);
                        var euqlidDistanceTraveled = 0;
                        var realDistanceTraveled = 0;
                        var xdistance = 0;
                        var ydistance = 0;
                        var currentDistanceTraveled = 0;
                        if (closedStack.Count > 0 && visitedNodesStack.Count > 0)
                        {
                            //lastVisitedNode = closedStack.Peek();
                            lastVisitedNode = visitedNodesStack.Peek();
                        }
                        else
                        {
                            lastVisitedNode = new SearchNode(startPosition, Map.StepDistance(map.StartTile, map.EndTile), 0);
                        }
                        foreach(SearchNode node in openStack)
                        {
                            euqlidDistanceTraveled = ((startPosition.X - node.Position.X) * (startPosition.X - node.Position.X)
                                + ((startPosition.Y-node.Position.Y)*(startPosition.Y-node.Position.Y)));
                            realDistanceTraveled = visitedNodesStack.Count;
                            currentDistanceTraveled = node.DistanceTraveled;
                            xdistance = node.Position.X;
                            ydistance = node.Position.Y;
                            if (currentDistanceTraveled > lastVisitedNode.DistanceTraveled)
                            {
                                visitedNodesStack.Push(node);
                                result = node;
                                break;
                            }
                        }
                        if (currentDistanceTraveled <= lastVisitedNode.DistanceTraveled)
                        {
                            if (visitedNodesStack.Count > 1)
                            {
                                visitedNodesStack.Pop();
                            }

                            result = lastVisitedNode;
                            //visitedNodesStack.Push(result);
                        }
                        success = true;
                        break;
                    // Best first search always looks at whatever path is closest to
                    // the goal regardless of how long that path is.
                    case SearchMethodEnum.BestFirst:
                        totalSearchSteps++;
                        foreach (SearchNode node in openList)
                        {
                            currentDistance = node.DistanceToGoal;
                            if (currentDistance < smallestDistance)
                            {
                                success = true;
                                result = node;
                                smallestDistance = currentDistance;
                            }
                        }
                        break;
                    // A* search uses a heuristic, an estimate, to try to find the 
                    // best path to take. As long as the heuristic is admissible, 
                    // meaning that it never over-estimates, it will always find 
                    // the best path.
                    case SearchMethodEnum.AStar:
                        totalSearchSteps++;
                        foreach (SearchNode node in openList)
                        {
                            currentDistance = Heuristic(node);
                            // The heuristic value gives us our optimistic estimate 
                            // for the path length, while any path with the same 
                            // heuristic value is equally ‘good’ in this case we’re 
                            // favoring paths that have the same heuristic value 
                            // but are longer.
                            if (currentDistance <= smallestDistance)
                            {
                                if (currentDistance < smallestDistance)
                                {
                                    success = true;
                                    result = node;
                                    smallestDistance = currentDistance;
                                }
                                else if (currentDistance == smallestDistance &&
                                    node.DistanceTraveled > result.DistanceTraveled)
                                {
                                    success = true;
                                    result = node;
                                    smallestDistance = currentDistance;
                                }
                            }
                        }
                        break;
                }
            }
            return success;
        }

        /// <summary>
        /// Generates an optimistic estimate of the total path length to the goal 
        /// from the given position.
        /// </summary>
        /// <param name="location">Location to examine</param>
        /// <returns>Path length estimate</returns>
        private static float Heuristic(SearchNode location)
        {
            return location.DistanceTraveled + location.DistanceToGoal;
        }

        /// <summary>
        /// Generates the path from start to end.
        /// </summary>
        /// <returns>The path from start to end</returns>
        public LinkedList<Point> FinalPath()
        {
            LinkedList<Point> path = new LinkedList<Point>();
            if (searchStatus == SearchStatusEnum.PathFound)
            {
                Point curPrev = map.EndTile;
                path.AddFirst(curPrev);
                while (paths.ContainsKey(curPrev))
                {
                    curPrev = paths[curPrev];
                    path.AddFirst(curPrev);
                }
            }
            return path;
        }

        #endregion
    }
}
