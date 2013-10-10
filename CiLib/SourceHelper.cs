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
using System.Collections;
using System.Collections.Generic;

namespace Foxoft.Ci {

  public struct TypeMappingInfo {
    public CiType Type;
    public string Name;
    public string Definition;
    public bool Native;
    public string Null;
    public string NullInit;
    public string Init;
    public int level;
    public string ItemType;
    public string ItemDefault;
  }

  public class TypeMapper {
    static private HashSet<CiClass> refClass = new HashSet<CiClass>();
    static private HashSet<CiType> refType = new HashSet<CiType>();
    static private Dictionary<CiType, TypeMappingInfo> TypeCache = new Dictionary<CiType, TypeMappingInfo>();

    static public HashSet<CiClass>GetClassList() {
      return refClass;
    }

    static public HashSet<CiType>GetTypeList() {
      return refType;
    }

    static public string GetTypeName(CiType type) {
      return GetTypeInfo(type).Name;
    }

    static public TypeMappingInfo GetTypeInfo(CiType type) {
      if (TypeCache.ContainsKey(type)) {
        return TypeCache[type];
      }
      TypeMappingInfo info = DecodeType(type);
      TypeCache.Add(type, info);
      return info;
    }

    static public TypeMappingInfo DecodeType(CiType type) {
      TypeMappingInfo info = new TypeMappingInfo();
      info.Type = type;
      info.Native = true;
      info.level = 0;
      StringBuilder name = new StringBuilder();
      StringBuilder def = new StringBuilder();
      StringBuilder nul = new StringBuilder();
      StringBuilder nulInit = new StringBuilder();
      StringBuilder init = new StringBuilder();
      CiType elem = type;
      if (type.ArrayLevel > 0) {
        nul.Append("EMPTY_");
        nulInit.Append("SetLength({3}");
        init.Append("SetLength([0]");
        for (int i = 0; i < type.ArrayLevel; i++) {
          def.Append("array of ");
          name.Append("ArrayOf_");
          nul.Append("ArrayOf_");
          nulInit.Append(", 0");
          init.Append(", [" + (i + 1) + "]");
        }
        info.Native = false;
        nulInit.Append(")");
        init.Append(")");
        info.level = type.ArrayLevel;
        elem = type.BaseType;
      }
      if (elem is CiStringType) {
        name.Append("string");
        def.Append("string");
        info.ItemDefault = "''";
        info.ItemType = "string";
        if (info.Native) {
          nul.Append("''");
        }
        else {
          nul.Append("string");
        }
      }
      else if (elem == CiBoolType.Value) {
        name.Append("boolean");
        def.Append("boolean");
        info.ItemDefault = "false";
        info.ItemType = "boolean";
        if (info.Native) {
          nul.Append("''");
        }
        else {
          nul.Append("boolean");
        }
      }
      else if (elem == CiByteType.Value) {
        name.Append("byte");
        def.Append("byte");
        info.ItemDefault = "0";
        info.ItemType = "byte";
        if (info.Native) {
          nul.Append("0");
        }
        else {
          nul.Append("byte");
        }
      }
      else if (elem == CiIntType.Value) {
        name.Append("integer");
        def.Append("integer");
        info.ItemDefault = "0";
        info.ItemType = "integer";
        if (info.Native) {
          nul.Append("0");
        }
        else {
          nul.Append("integer");
        }
      }
      else if (elem is CiEnum) {
        name.Append(elem.Name);
        def.Append(elem.Name);
        var ev = ((CiEnum)elem).Values[0];
        info.ItemDefault = ev.Type.Name + "." + ev.Name;
        if (info.Native) {
          nul.Append(info.ItemDefault);
        }
        else {
          nul.Append(elem.Name);
        }
        info.ItemType = elem.Name;
      }
      else {
        name.Append(elem.Name);
        def.Append(elem.Name);
        info.ItemDefault = "nil";
        if (info.Native) {
          nul.Append("nil");
        }
        else {
          nul.Append(elem.Name);
        }
        info.ItemType = elem.Name;
      }
      info.Name = name.ToString();
      info.Definition = def.ToString();
      info.Null = nul.ToString();
      info.NullInit = (nulInit.Length > 0 ? String.Format(nulInit.ToString(), info.Name, info.Definition, info.ItemType, info.Null).Replace('[', '{').Replace(']', '}') : null);
      info.Init = (nulInit.Length > 0 ? String.Format(init.ToString(), info.Name, info.Definition, info.ItemType, info.Null) : null);
      if ((!info.Native) && (info.Null != null)) {

        /*TODO decommentare
        if (!IsReservedWord(info.Null)) {
          SymbolMapper.ReservedWords.Add(info.Null);
        }
*/
      }
      return info;
    }

    static public void AddType(CiType type) {
      if (type == null) {
        return;
      }
      if (refType.Contains(type)) {
        return;
      }
      if (type is CiArrayType) {
        CiArrayType arr = (CiArrayType)type;
        if (arr.BaseType is CiClassType) {
          CiClass klass = ((CiClassType)arr.BaseType).Class;
          if (!refClass.Contains(klass)) {
            refClass.Add(klass);
          }
        }
      }
      if (type is CiClassType) {
        CiClass klass = ((CiClassType)type).Class;
        if (!refClass.Contains(klass)) {
          refClass.Add(klass);
        }
      }
      refType.Add(type);
    }

    static public void Reset() {
      refClass.Clear();
      refType.Clear();
      TypeCache.Clear();
    }
  }

  public class NoIIFExpand {

    static private Stack<int> instructions = new Stack<int>();

    static public void Reset() {
      instructions.Clear();
    }

    static public void Push(int step) {
      instructions.Push(step);
    }

    static public void Pop() {
      instructions.Pop();
    }

    static public bool In(int inst) {
      return instructions.Any(step => step == inst);
    }
  }

  public class MethodStack {

    static private Stack<CiMethod> methods = new Stack<CiMethod>();

    static public void Reset() {
      methods.Clear();
    }

    static public void Push(CiMethod call) {
      methods.Push(call);
    }

    static public void Pop() {
      methods.Pop();
    }

    static public CiMethod Peek() {
      if (methods.Count > 0) {
        return methods.Peek();
      }
      return null;
    }
  }

  public class ExprType {
    static private Dictionary<CiExpr, CiType> exprMap = new  Dictionary<CiExpr, CiType>();

    static public void Reset() {
      exprMap.Clear();
    }

    static public CiType Get(CiExpr expr) {
      CiType result;
      exprMap.TryGetValue(expr, out result);
      if (result == null) {
        result = Analyze(expr);
        exprMap.Add(expr, result);
      }
      return result;
    }

    static public CiType Analyze(CiExpr expr) {
      if (expr == null)
        return CiType.Null;
      else if (expr is CiUnaryExpr) {
        var e = (CiUnaryExpr)expr;
        CiType t = Get(e.Inner);
        return t;
      }
      else if (expr is CiPostfixExpr) {
        var e = (CiPostfixExpr)expr;
        CiType t = Get(e.Inner);
        return t;
      }
      else if (expr is CiBinaryExpr) {
        var e = (CiBinaryExpr)expr;
        CiType left = Get(e.Left);
        CiType right = Get(e.Right);
        CiType t = ((left == null) || (left == CiType.Null)) ? right : left;
        exprMap[e.Left] = t;
        exprMap[e.Right] = t;
        return t;
      }
      else {
        return expr.Type;
      }
    }
  }

  public class BreakExit {
    //
    static private Dictionary<ICiStatement, BreakExit> mapping = new  Dictionary<ICiStatement, BreakExit>();
    static private Dictionary<CiMethod, List<BreakExit>> methods = new Dictionary<CiMethod, List<BreakExit>>();
    static private Stack<BreakExit> exitPoints = new Stack<BreakExit>();
    //
    public string Name;

    public BreakExit(string aName) {
      this.Name = aName;
    }

    static public void AddSwitch(CiMethod method, CiSwitch aSymbol) {
      List<BreakExit> labels = null;
      methods.TryGetValue(method, out labels);
      if (labels == null) {
        labels = new List<BreakExit>();
        methods.Add(method, labels);
      }
      BreakExit label = new BreakExit("goto__" + labels.Count);
      labels.Add(label);
      mapping.Add(aSymbol, label);
    }

    static public List<BreakExit> GetLabels(CiMethod method) {
      List<BreakExit> labels = null;
      methods.TryGetValue(method, out labels);
      return labels;
    }

    static public BreakExit GetLabel(ICiStatement stmt) {
      BreakExit label = null;
      mapping.TryGetValue(stmt, out label);
      return label;
    }

    static public void Reset() {
      mapping.Clear();
      methods.Clear();
    }

    static public BreakExit Push(ICiStatement stmt) {
      BreakExit label = GetLabel(stmt);
      exitPoints.Push(label);
      return label;
    }

    static public void Pop() {
      exitPoints.Pop();
    }

    static public BreakExit Peek() {
      if (exitPoints.Count == 0) {
        return null;
      }
      return exitPoints.Peek();
    }
  }
}