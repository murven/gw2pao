﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Input;
using GW2PAO.API.Services.Interfaces;
using GW2PAO.Modules.Tasks;
using GW2PAO.Modules.Tasks.Interfaces;
using GW2PAO.Modules.Tasks.ViewModels;
using GW2PAO.Utility;
using Microsoft.Practices.Prism.Mvvm;
using NLog;

namespace GW2PAO.Modules.Map.ViewModels
{
    [Export(typeof(PlayerMarkersViewModel))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PlayerMarkersViewModel : BindableBase
    {
        /// <summary>
        /// Default logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private TaskTrackerViewModel taskTrackerVm;
        private PlayerTasksFactory playerTaskFactory;
        private IPlayerTasksController tasksController;
        private ObservableCollection<PlayerTaskViewModel> playerTasksCollection;
        private IZoneService zoneService;
        private IPlayerService playerService;
        private MapUserData userData;

        /// <summary>
        /// The collection of player markers to show on the map
        /// </summary>
        public ObservableCollection<PlayerMarkerViewModel> PlayerMarkers
        {
            get;
            private set;
        }

        /// <summary>
        /// The collection of template player markers to show on the map
        /// </summary>
        public ObservableCollection<PlayerMarkerViewModel> MarkerTemplates
        {
            get;
            private set;
        }

        /// <summary>
        /// Command to load tasks/markers
        /// </summary>
        public ICommand LoadCommand { get { return this.taskTrackerVm.LoadTasksCommand; } }

        /// <summary>
        /// Command to import tasks/markers
        /// </summary>
        public ICommand ImportCommand { get { return this.taskTrackerVm.ImportTasksCommand; } }

        /// <summary>
        /// Command to export all current tasks/markers
        /// </summary>
        public ICommand ExportCommand { get { return this.taskTrackerVm.ExportTasksCommand; } }

        /// <summary>
        /// Command to delete all current tasks/markers
        /// </summary>
        public ICommand DeleteAllCommand { get { return this.taskTrackerVm.DeleteAllCommand; } }


        /// <summary>
        /// Constructs a new MarkersViewModel object
        /// </summary>
        [ImportingConstructor]
        public PlayerMarkersViewModel(TaskTrackerViewModel taskTrackerVm,
            MapUserData userData,
            PlayerTasksFactory playerTaskFactory,
            IPlayerTasksController tasksController,
            IZoneService zoneService,
            IPlayerService playerService)
        {
            this.taskTrackerVm = taskTrackerVm;
            this.playerTaskFactory = playerTaskFactory;
            this.tasksController = tasksController;
            this.zoneService = zoneService;
            this.playerService = playerService;
            this.userData = userData;

            this.PlayerMarkers = new ObservableCollection<PlayerMarkerViewModel>();

            this.playerTasksCollection = (ObservableCollection<PlayerTaskViewModel>)this.taskTrackerVm.PlayerTasks.Source;
            foreach (var task in this.playerTasksCollection)
            {
                task.PropertyChanged += Task_PropertyChanged;
                if (task.HasContinentLocation)
                    this.PlayerMarkers.Add(new PlayerMarkerViewModel(task, this.zoneService, this.playerService));
            }
            this.playerTasksCollection.CollectionChanged += PlayerTasksCollection_CollectionChanged;

            this.InitializeTemplates();
            this.PlayerMarkers.CollectionChanged += PlayerMarkers_CollectionChanged;
        }

        private void InitializeTemplates()
        {
            List<string> templateIcons = new List<string>()
            {
                @"/Images/Map/Markers/miningNode.png",
                @"/Images/Map/Markers/harvestingNode.png",
                @"/Images/Map/Markers/loggingNode.png",
                @"/Images/Map/Markers/activity.png",
                @"/Images/Map/Markers/adventure.png",
                @"/Images/Map/Markers/anvil.png",
                @"/Images/Map/Markers/book.png",
                @"/Images/Map/Markers/parchment.png",
                @"/Images/Map/Markers/dragon.png",
                @"/Images/Map/Markers/greenFlag.png",
                @"/Images/Map/Markers/quaggan.png",
                @"/Images/Map/Markers/trophy.png",
                @"/Images/Map/Markers/pointA.png",
                @"/Images/Map/Markers/pointB.png",
                @"/Images/Map/Markers/pointC.png",
                @"/Images/Map/Markers/orangeShield.png",
                @"/Images/Map/Markers/redShield.png",
                @"/Images/Map/Markers/blueStar.png",
                @"/Images/Map/Markers/greenStar.png",
                @"/Images/Map/Markers/yellowStar.png",
                @"/Images/Map/Markers/yellowStar2.png",
                @"/Images/Map/Markers/downedAlly.png",
                @"/Images/Map/Markers/downedEnemy.png",
                @"/Images/Map/Markers/blueSiege.png",
                @"/Images/Map/Markers/redSiege.png",
                @"/Images/Map/Markers/swords.png"
            };

            this.MarkerTemplates = new ObservableCollection<PlayerMarkerViewModel>();
            foreach (var icon in templateIcons)
            {
                var task = this.playerTaskFactory.GetPlayerTask();
                task.IconUri = icon;
                var vm = this.playerTaskFactory.GetPlayerTaskViewModel(task);
                this.MarkerTemplates.Add(new PlayerMarkerViewModel(vm, this.zoneService, this.playerService));
            }
        }

        private void PlayerMarkers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // When a player marker is added, check to see if we need to reset the template for that marker
            // (for example, when the user added the marker by drag/dropping it onto the map)
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (PlayerMarkerViewModel newItem in e.NewItems)
                {
                    var template = this.MarkerTemplates.FirstOrDefault(m => m == newItem);
                    if (template != null)
                    {
                        // This marker came from a template

                        // Replace the template
                        var idx = this.MarkerTemplates.IndexOf(template);
                        this.MarkerTemplates.Remove(template);
                        var task = this.playerTaskFactory.GetPlayerTask();
                        task.IconUri = newItem.Icon;
                        var vm = this.playerTaskFactory.GetPlayerTaskViewModel(task);
                        var newTemplate = new PlayerMarkerViewModel(vm, this.zoneService, this.playerService);
                        this.MarkerTemplates.Insert(idx, newTemplate);

                        // Then add the corresponding task
                        this.tasksController.AddOrUpdateTask(newItem.TaskViewModel);
                    }
                }
            }
        }

        private void PlayerTasksCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (PlayerTaskViewModel taskVm in e.NewItems)
                    {
                        taskVm.PropertyChanged += Task_PropertyChanged;
                        if (taskVm.HasContinentLocation)
                        {
                            var playerMarker = this.PlayerMarkers.FirstOrDefault(m => m.ID.Equals(taskVm.Task.ID));
                            if (playerMarker == null)
                                this.PlayerMarkers.Add(new PlayerMarkerViewModel(taskVm, this.zoneService, this.playerService));
                        }
                    }                    
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (PlayerTaskViewModel taskVm in e.OldItems)
                    {
                        taskVm.PropertyChanged -= Task_PropertyChanged;
                        var playerMarker = this.PlayerMarkers.FirstOrDefault(m => m.ID.Equals(taskVm.Task.ID));
                        if (playerMarker != null)
                            this.PlayerMarkers.Remove(playerMarker);
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (PlayerTaskViewModel taskVm in e.NewItems)
                    {
                        taskVm.PropertyChanged += Task_PropertyChanged;
                        if (taskVm.HasContinentLocation)
                        {
                            var playerMarker = this.PlayerMarkers.FirstOrDefault(m => m.ID.Equals(taskVm.Task.ID));
                            if (playerMarker == null)
                                this.PlayerMarkers.Add(new PlayerMarkerViewModel(taskVm, this.zoneService, this.playerService));
                        }
                    }
                    foreach (PlayerTaskViewModel taskVm in e.OldItems)
                    {
                        taskVm.PropertyChanged -= Task_PropertyChanged;
                        var playerMarker = this.PlayerMarkers.FirstOrDefault(m => m.ID.Equals(taskVm.Task.ID));
                        if (playerMarker != null)
                            this.PlayerMarkers.Remove(playerMarker);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var marker in this.PlayerMarkers)
                    {
                        marker.TaskViewModel.PropertyChanged -= Task_PropertyChanged;
                    }
                    this.PlayerMarkers.Clear();
                    break;
                default:
                    break;
            }
        }

        private void Task_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "HasContinentLocation")
            {
                var taskVm = (PlayerTaskViewModel)sender;
                if (taskVm.HasContinentLocation)
                {
                    var playerMarker = this.PlayerMarkers.FirstOrDefault(m => m.ID.Equals(taskVm.Task.ID));
                    if (playerMarker == null)
                    {
                        this.PlayerMarkers.Add(new PlayerMarkerViewModel(taskVm, this.zoneService, this.playerService));
                    }
                }
                else
                {
                    var playerMarker = this.PlayerMarkers.FirstOrDefault(m => m.ID.Equals(taskVm.Task.ID));
                    if (playerMarker != null)
                    {
                        this.PlayerMarkers.Remove(playerMarker);
                    }
                }
            }
        }
    }
}
