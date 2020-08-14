﻿using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
using Synthesis.Bethesda.Execution.Settings;
using Noggog;
using Noggog.WPF;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using Synthesis.Bethesda.Execution.Patchers;
using Buildalyzer;
using System.Linq;
using DynamicData.Binding;
using Synthesis.Bethesda.Execution;

namespace Synthesis.Bethesda.GUI
{
    public class SolutionPatcherVM : PatcherVM
    {
        public PathPickerVM SolutionPath { get; } = new PathPickerVM()
        {
            ExistCheckOption = PathPickerVM.CheckOptions.On,
            PathType = PathPickerVM.PathTypeOptions.File,
        };

        public IObservableCollection<string> ProjectsDisplay { get; }

        [Reactive]
        public string ProjectSubpath { get; set; } = string.Empty;

        public PathPickerVM SelectedProjectPath { get; } = new PathPickerVM()
        {
            ExistCheckOption = PathPickerVM.CheckOptions.On,
            PathType = PathPickerVM.PathTypeOptions.File,
        };

        private readonly ObservableAsPropertyHelper<string> _DisplayName;
        public override string DisplayName => _DisplayName.Value;

        public override bool NeedsConfiguration => true;

        private readonly ObservableAsPropertyHelper<IErrorResponse> _CanCompleteConfiguration;
        public override IErrorResponse CanCompleteConfiguration => _CanCompleteConfiguration.Value;

        private readonly ObservableAsPropertyHelper<ConfigurationStateVM> _State;
        public override ConfigurationStateVM State => _State.Value;

        public SolutionPatcherVM(ProfileVM parent, SolutionPatcherSettings? settings = null)
            : base(parent, settings)
        {
            CopyInSettings(settings);
            SolutionPath.Filters.Add(new CommonFileDialogFilter("Solution", ".sln"));
            SelectedProjectPath.Filters.Add(new CommonFileDialogFilter("Project", ".csproj"));

            _DisplayName = this.WhenAnyValue(
                x => x.Nickname,
                x => x.SolutionPath.TargetPath,
                (nickname, path) =>
                {
                    if (!string.IsNullOrWhiteSpace(nickname)) return nickname;
                    try
                    {
                        var name = Path.GetFileName(Path.GetDirectoryName(path));
                        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
                        return name;
                    }
                    catch (Exception)
                    {
                        return string.Empty;
                    }
                })
                .ToGuiProperty<string>(this, nameof(DisplayName));

            ProjectsDisplay = this.WhenAnyValue(x => x.SolutionPath.TargetPath)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(x =>
                {
                    if (!File.Exists(x)) return Enumerable.Empty<string>();
                    try
                    {
                        var manager = new AnalyzerManager(x);
                        return manager.Projects.Keys.Select(projPath => projPath.TrimStart($"{Path.GetDirectoryName(x)}\\"!));
                    }
                    catch (Exception)
                    {
                        return Enumerable.Empty<string>();
                    }
                })
                .Select(x => x.AsObservableChangeSet())
                .Switch()
                .ObserveOnGui()
                .ToObservableCollection(this);

            this.WhenAnyValue(x => x.ProjectSubpath)
                .DistinctUntilChanged()
                .CombineLatest(this.WhenAnyValue(x => x.SolutionPath.TargetPath)
                        .DistinctUntilChanged(),
                    (subPath, slnPath) =>
                    {
                        if (subPath == null || slnPath == null) return string.Empty;
                        try
                        {
                            return Path.Combine(Path.GetDirectoryName(slnPath)!, subPath);
                        }
                        catch (Exception)
                        {
                            return string.Empty;
                        }
                    })
                .Subscribe(p => SelectedProjectPath.TargetPath = p)
                .DisposeWith(this);

            //this.WhenAnyValue(x => x.SolutionPath.TargetPath)
            //    .CombineLatest(this.WhenAnyValue(x => x.SolutionPath.InError),
            //        (path, err) => (path, err))
            //    .Subscribe(i =>
            //    {
            //        if (i.err || !File.Exists(i.path)) return;
            //        var manager = new AnalyzerManager();
            //        var proj = manager.GetProject(i.path);
            //        var results = proj.Build();
            //        int wer = 23;
            //        wer++;
            //    });


            _State = Observable.CombineLatest(
                    this.WhenAnyValue(x => x.SolutionPath.ErrorState),
                    this.WhenAnyValue(x => x.SelectedProjectPath.ErrorState),
                    (sln, proj) =>
                    {
                        if (sln.Failed) return sln;
                        return proj;
                    })
                .Select(err =>
                {
                    return new ConfigurationStateVM()
                    {
                        IsHaltingError = err.Failed,
                        RunnableState = err,
                    };
                })
                .ToGuiProperty<ConfigurationStateVM>(this, nameof(State), ConfigurationStateVM.Success);

            _CanCompleteConfiguration = this.WhenAnyValue(x => x.State)
                .Select(s => s.RunnableState)
                .ToGuiProperty(this, nameof(CanCompleteConfiguration), ErrorResponse.Success);
        }

        public override PatcherSettings Save()
        {
            var ret = new SolutionPatcherSettings();
            CopyOverSave(ret);
            ret.SolutionPath = this.SolutionPath.TargetPath;
            ret.ProjectSubpath = this.ProjectSubpath;
            return ret;
        }

        private void CopyInSettings(SolutionPatcherSettings? settings)
        {
            if (settings == null) return;
            this.SolutionPath.TargetPath = settings.SolutionPath;
            this.ProjectSubpath = settings.ProjectSubpath;
        }

        public override PatcherRunVM ToRunner(PatchersRunVM parent)
        {
            return new PatcherRunVM(
                parent,
                this,
                new SolutionPatcherRun(
                    nickname: DisplayName,
                    pathToSln: SolutionPath.TargetPath, 
                    pathToProj: SelectedProjectPath.TargetPath));
        }
    }
}
