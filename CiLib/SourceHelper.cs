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