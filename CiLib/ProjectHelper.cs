// ProjectHelper.cs - Helper classes for project translation
//
// Copyright (C) 2013  Enrico Croce
//
// This file is part of CiTo, see http://cito.sourceforge.net
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Foxoft.Ci;

namespace Foxoft.Ci {

  public class GeneratorInfo {
    public string ID;
    public string Language;
    public string Extension;
    public bool SplitFile;
    public IGenerator Generator;

    public GeneratorInfo(string ID, string language, string extension, bool splitFile, IGenerator generator) {
      this.ID = ID;
      this.Language = language;
      this.Extension = extension;
      this.SplitFile = splitFile;
      this.Generator = generator;
    }
  }

  public class GeneratorHelper {

    private static Dictionary<string, GeneratorInfo> Generators = new Dictionary<string, GeneratorInfo>();

    static GeneratorHelper() {
      Add(new GeneratorInfo("10", "Object Pascal", "pas", false, new GenPas()));
      Add(new GeneratorInfo("20", "PHP", "php", false, new GenPHP()));
      Add(new GeneratorInfo("30", "Java", "java", true, new GenJava()));
      Add(new GeneratorInfo("40", "C89", "c", false, new GenC89()));
      Add(new GeneratorInfo("41", "C99", "c99", false, new GenC()));
      Add(new GeneratorInfo("50", "D", "d", false, new GenD()));
      Add(new GeneratorInfo("60", "C#", "cs", false, new GenCs()));
      Add(new GeneratorInfo("70", "Perl 5.8", "pm", false, new GenPerl58()));
      Add(new GeneratorInfo("71", "Perl 5.10", "pm510", false, new GenPerl510()));
      Add(new GeneratorInfo("80", "JavaScript", "js", false, new GenJs()));
      Add(new GeneratorInfo("81", "JavaScript (Typed Arrays)", "js-ta", false, new GenJsWithTypedArrays()));
      Add(new GeneratorInfo("90", "Action Script", "as", true, new GenAs()));
      Add(new GeneratorInfo("99", "Ć", "ci", true, new CiGenerator()));
    }

    protected static void Add(GeneratorInfo generator) {
      Generators.Add(generator.Language, generator);
    }

    public static GeneratorInfo GetGenerator(string Language) {
      GeneratorInfo result = null;
      if (Language != null) {
        Generators.TryGetValue(Language, out result);
      }
      return result;
    }

    public static string[] GetLanguages() {
      return Generators.Select(x => x.Value).OrderBy(x => x.ID).Select(x => x.Language).ToArray();
    }

    public static GeneratorInfo[] GetGenerators() {
      return Generators.Select(x => x.Value).OrderBy(x => x.Extension).ToArray();
    }
  }

  public class ProjectFile {
    public string Path;
    public string Name;
    public string Code;
    public bool Changed;

    public string GetDir() {
      return System.IO.Path.GetDirectoryName(Path);
    }
  }

  public class ProjectFiles {
    public Dictionary<string, ProjectFile> Source = new Dictionary<string, ProjectFile>();
    public Dictionary<string, ProjectFile> Target = new Dictionary<string, ProjectFile>();

    public ProjectFiles() {
      Reset();
    }

    public void Reset() {
      Source.Clear();
      Target.Clear();
      ProjectFile file = new ProjectFile();
      file.Path = "." + System.IO.Path.DirectorySeparatorChar + "noname.ci";
      file.Name = "noname.ci";
      file.Code = "//CiTo Source code";
      file.Changed = false;
      Source.Add(file.Name, file);
    }

    public string[] GetSourceDirs() {
      return Source.Select(x => x.Value.GetDir()).OrderBy(x => x).ToArray();
    }

    public string[] GetSources() {
      return Source.Keys.OrderBy(x => x).ToArray();
    }

    public string[] GetTargets() {
      return Target.Keys.OrderBy(x => x).ToArray();
    }

    TextWriter CreateTargetWriter(string filename) {
      return new CiTargetWriter(this, filename);
    }

    public void LoadFiles(string[] Filenames) {
      int len = Filenames.Length;
      if (len < 1) {
        Reset();
      }
      else {
        Source.Clear();
        Target.Clear();
        for (int i=0; i<len; i++) {
          ProjectFile file = new ProjectFile();
          file.Path = Filenames[i];
          file.Name = System.IO.Path.GetFileName(file.Path);
          file.Code = System.IO.File.ReadAllText(file.Path);
          file.Changed = false;
          Source.Add(file.Name, file);
        }
      }
    }

    public void GenerateTarget(string Language, string NameSpace) {
      Target.Clear();
      if (Language == null) {
        return;
      }
      GeneratorInfo generator = GeneratorHelper.GetGenerator(Language);
      if (generator == null) {
        return;
      }
      CiParser parser = new CiParser();
      foreach (ProjectFile file in Source.Values) {
        parser.Parse(file.Name, new StringReader(file.Code));
      }
      CiProgram program = parser.Program;
      CiResolver resolver = new CiResolver();
      resolver.SearchDirs = GetSourceDirs();
      resolver.Resolve(program);
      generator.Generator.SetTextWriterFactory(CreateTargetWriter);
      if (generator.SplitFile) {
        generator.Generator.SetOutputFile(".");
      }
      else {
        generator.Generator.SetOutputFile(Path.ChangeExtension(string.IsNullOrEmpty(NameSpace) ? "cito" : NameSpace, generator.Extension));
      }
      generator.Generator.SetNamespace(NameSpace);
      generator.Generator.Write(program);
    }
  }

  public class CiTargetWriter : StringWriter {

    string Name;
    ProjectFiles Project;

    public CiTargetWriter(ProjectFiles project, string name) {
      this.Project = project;
      this.Name = name == null ? "unknown" : name;
    }

    public override void Close() {
      base.Close();
      ProjectFile info = new ProjectFile();
      info.Name = this.Name;
      info.Path = "." + System.IO.Path.DirectorySeparatorChar + info.Name;
      info.Code = base.ToString();
      info.Changed = true;
      Project.Target.Add(info.Name, info);
    }
  }
}