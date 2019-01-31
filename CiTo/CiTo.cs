// CiTo.cs - Ci translator
//
// Copyright (C) 2011-2013  Piotr Fusik
// Copyright (C) 2013-2014  Enrico Croce
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("CiTo")]
[assembly: AssemblyDescription("Ci Translator")]
namespace Foxoft.Ci {

  public sealed class CiTo {
    CiTo() {
    }

    static void Usage() {
      string me = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
      Console.WriteLine("Usage: " + me + " [OPTIONS] -o FILE INPUT.ci");
      Console.WriteLine("Options:");
      Console.WriteLine("--help     Display this information");
      Console.WriteLine("--version  Display version information");
      Console.WriteLine("-o FILE    Write to the specified file");
      Console.WriteLine("-n NAME    Specify namespace, package or unit for appropriate languages");
      Console.WriteLine("-D NAME    Define conditional compilation symbol");
      Console.WriteLine("-I DIR     Add directory to BinaryResource search path");
      foreach (GeneratorInfo info in gens) {
        Console.WriteLine("-l {0,-7} Translate to {1}", info.Extension, info.Language);
      }
    }

    static GeneratorInfo[] gens = GeneratorHelper.GetGenerators();

    public static int Main(string[] args) {
      HashSet<string> preSymbols = new HashSet<string>();
      preSymbols.Add("true");
      List<string> inputFiles = new List<string>();
      List<string> searchDirs = new List<string>();
      string lang = null;
      string outputFile = null;
      string aNamespace = null;
      for (int i = 0; i < args.Length; i++) {
        string arg = args[i];
        if (arg[0] == '-') {
          switch (arg) {
            case "--help":
              Usage();
              return 0;
            case "--version":
              string me = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
              string ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); 
              Console.WriteLine(me + " " + ver);
              return 0;
            case "-l":
              lang = args[++i];
              break;
            case "-o":
              outputFile = args[++i];
              break;
            case "-n":
              aNamespace = args[++i];
              break;
            case "-D":
              string symbol = args[++i];
              if (symbol == "true" || symbol == "false") {
                throw new ArgumentException(symbol + " is reserved");
              }
              preSymbols.Add(symbol);
              break;
            case "-I":
              searchDirs.Add(args[++i]);
              break;
            default:
              throw new ArgumentException("Unknown option: " + arg);
          }
        }
        else {
          inputFiles.Add(arg);
        }
      }
      if (lang == null && outputFile != null) {
        string ext = Path.GetExtension(outputFile);
        if (ext.Length >= 2) {
          lang = ext.Substring(1);
        }
      }
      if (lang == null || outputFile == null || inputFiles.Count == 0) {
        Usage();
        return 1;
      }
      CiParser parser = new CiParser();
      parser.PreSymbols = preSymbols;
      foreach (string inputFile in inputFiles) {
        try {
          parser.Parse(inputFile, File.OpenText(inputFile));
        }
        catch (Exception ex) {
          Console.Error.WriteLine("{0}({1}): ERROR: {2}", inputFile, parser.InputLineNo, ex.Message);
          parser.PrintMacroStack();
          if (parser.CurrentMethod != null) {
            Console.Error.WriteLine("   in method {0}", parser.CurrentMethod.Name);
          }
          return 1;
        }
      }
      CiProgram program = parser.Program;
      CiResolver resolver = new CiResolver();
      resolver.SearchDirs = searchDirs;
      try {
        resolver.Resolve(program);
      }
      catch (Exception ex) {
        if (resolver.CurrentClass != null) {
          Console.Error.Write(resolver.CurrentClass.SourceFilename);
          Console.Error.Write(": ");
        }
        Console.Error.WriteLine("ERROR: {0}", ex.Message);
        if (resolver.CurrentMethod != null) {
          Console.Error.WriteLine("   in method {0}", resolver.CurrentMethod.Name);
        }
        return 1;
      }

      IGenerator gen = null;
      foreach (GeneratorInfo info in gens) {
        if (info.Extension.Equals(lang)) {
          gen = info.Generator;
          break;
        }
      }
      if (gen == null) {
        throw new ArgumentException("Unknown language: " + lang);
      }
      gen.SetOutputFile(outputFile);
      gen.SetNamespace(aNamespace);
      gen.WriteProgram(program);
      return 0;
    }
  }
}