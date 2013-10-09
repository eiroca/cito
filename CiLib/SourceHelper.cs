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

  public class SymbolMapper {
    //
    static private int suffix = 0;
    static private Dictionary<CiSymbol, SymbolMapper> varMap = new  Dictionary<CiSymbol, SymbolMapper>();
    static public HashSet<string> ReservedWords = null;
    //
    public CiSymbol Symbol = null;
    public string NewName = "?";
    public SymbolMapper Parent = null;
    public List<SymbolMapper> childs = new List<SymbolMapper>();

    static public void SetReservedWords(string[] words) {
      ReservedWords = new HashSet<string>(words);
    }

    static public void Reset() {
      suffix = 0;
      varMap.Clear();
    }

    static public bool IsEmpty() {
      return varMap.Count == 0;
    }

    static public SymbolMapper AddSymbol(SymbolMapper aParent, CiSymbol aSymbol) {
      return AddSymbol(aParent, aSymbol, true);
    }

    static public SymbolMapper AddSymbol(SymbolMapper aParent, CiSymbol aSymbol, bool inParentCheck) {
      SymbolMapper item = new SymbolMapper(aParent, aSymbol, inParentCheck);
      if (aSymbol != null) {
        varMap.Add(aSymbol, item);
      }
      return item;
    }

    static public SymbolMapper Find(CiSymbol symbol) {
      SymbolMapper result = null;
      varMap.TryGetValue(symbol, out result);
      return result;
    }

    static public string GetPascalName(string name) {
      StringBuilder tmpName = new StringBuilder(name.Length);
      foreach (char c in name) {
        tmpName.Append(CiLexer.IsLetter(c) ? c : '_');
      }
      string baseName = tmpName.ToString();
      if (ReservedWords.Contains(baseName.ToLower())) {
        baseName = "a" + baseName;
      }
      return baseName;
    }

    public SymbolMapper(CiSymbol aSymbol) {
      this.Symbol = aSymbol;
    }

    public SymbolMapper(SymbolMapper aParent, CiSymbol aSymbol, bool inParentCheck) {
      this.Symbol = aSymbol;
      this.Parent = aParent;
      if (aParent != null) {
        aParent.childs.Add(this);
      }
      this.NewName = this.GetUniqueName(inParentCheck);
    }

    public string GetUniqueName(bool inParentCheck) {
      if (this.Symbol == null) {
        return "?";
      }
      string baseName = GetPascalName(this.Symbol.Name);
      SymbolMapper context = this.Parent;
      while (context!=null) {
        foreach (SymbolMapper item in context.childs) {
          if (String.Compare(item.NewName, baseName, true) == 0) {
            //TODO Generate a real unique name
            suffix++;
            return baseName + "__" + suffix;
          }
        }
        if (inParentCheck) {
          context = context.Parent;
        }
        else {
          context = null;
        }
      }
      return baseName;
    }
  }

  public class ClassOrder {
    static private List<CiClass> order = new List<CiClass>();

    static public List<CiClass>GetList() {
      return order;
    }

    static public void AddClass(CiClass klass) {
      if (klass == null) {
        return;
      }
      if (order.Contains(klass)) {
        return;
      }
      AddClass(klass.BaseClass);
      order.Add(klass);
    }

    static public void Reset() {
      order.Clear();
    }
  }

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
        if (!SymbolMapper.ReservedWords.Contains(info.Null)) {
          SymbolMapper.ReservedWords.Add(info.Null);
        }
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
  #region Delegate helper
  //
  public delegate void WriteSymbolDelegate(CiSymbol statement);
  public delegate void WriteStatementDelegate(ICiStatement statement);
  public delegate void WriteExprDelegate(CiExpr expr);
  public delegate void WriteUnaryOperatorDelegate(CiUnaryExpr expr,UnaryOperatorInfo token);
  public delegate void WriteBinaryOperatorDelegate(CiBinaryExpr expr,BinaryOperatorInfo token);
  //
  public class OperatorInfo {
    public CiToken Token;
    public CiPriority Priority;
  }

  public class UnaryOperatorInfo: OperatorInfo {
    public string Prefix;
    public string Suffix;
    public WriteUnaryOperatorDelegate WriteDelegate;

    public UnaryOperatorInfo(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
      this.Token = token;
      this.Priority = priority;
      this.WriteDelegate = writeDelegate;
      this.Prefix = prefix;
      this.Suffix = suffix;
    }
  }

  public class BinaryOperatorInfo: OperatorInfo {
    public bool ForcePar;
    public string Symbol;
    public WriteBinaryOperatorDelegate WriteDelegate;

    public BinaryOperatorInfo(CiToken token, CiPriority priority, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      this.ForcePar = true;
      if ((token == CiToken.Plus) || (token == CiToken.Minus) || (token == CiToken.Asterisk) || (token == CiToken.Slash)) {
        this.ForcePar = false;
      }
      this.Token = token;
      this.Priority = priority;
      this.Symbol = symbol;
      this.WriteDelegate = writeDelegate;
    }
  }

  public class UnaryOperatorMetadata {

    protected Dictionary<CiToken, UnaryOperatorInfo> Metadata = new Dictionary<CiToken, UnaryOperatorInfo>();

    public UnaryOperatorMetadata() {
    }

    public void Add(CiToken token, CiPriority priority, WriteUnaryOperatorDelegate writeDelegate, string prefix, string suffix) {
      Metadata.Add(token, new UnaryOperatorInfo(token, priority, writeDelegate, prefix, suffix));
    }

    public UnaryOperatorInfo GetUnaryOperator(CiToken token) {
      UnaryOperatorInfo result = null;
      Metadata.TryGetValue(token, out result);
      if (result == null) {
        throw new InvalidOperationException("No delegate for " + token);
      }
      return result;
    }
  }

  public class BinaryOperatorMetadata {

    protected Dictionary<CiToken, BinaryOperatorInfo> Metadata = new Dictionary<CiToken, BinaryOperatorInfo>();

    public BinaryOperatorMetadata() {
    }

    public void Add(CiToken token, CiPriority priority, WriteBinaryOperatorDelegate writeDelegate, string symbol) {
      Metadata.Add(token, new BinaryOperatorInfo(token, priority, writeDelegate, symbol));
    }

    public BinaryOperatorInfo GetBinaryOperator(CiToken token) {
      BinaryOperatorInfo result = null;
      Metadata.TryGetValue(token, out result);
      if (result == null) {
        throw new InvalidOperationException("No delegate for " + token);
      }
      return result;
    }
  }

  public class ExpressionInfo {
    public CiPriority Priority;
    public WriteExprDelegate WriteDelegate;

    public ExpressionInfo(CiPriority priority, WriteExprDelegate writeDelegate) {
      this.Priority = priority;
      this.WriteDelegate = writeDelegate;
    }
  }

  public class ExpressionMetadata {
    protected Dictionary<Type, ExpressionInfo> Metadata = new Dictionary<Type, ExpressionInfo>();

    public ExpressionMetadata() {
    }

    public void Add(Type exprType, CiPriority priority, WriteExprDelegate writeDelegate) {
      Metadata.Add(exprType, new ExpressionInfo(priority, writeDelegate));
    }

    public ExpressionInfo GetInfo(CiExpr expr) {
      ExpressionInfo result = null;
      Type exprType = expr.GetType();
      while (exprType!=null) {
        Metadata.TryGetValue(exprType, out result);
        if (result != null) {
          break;
        }
        exprType = exprType.BaseType;
      }
      if (result == null) {
        throw new InvalidOperationException("No delegate for " + expr.GetType());
      }
      return result;
    }

    public virtual void Translate(CiExpr expr) {
      ExpressionInfo exprInfo = GetInfo(expr);
      if (exprInfo != null) {
        exprInfo.WriteDelegate(expr);
      }
      else {
        throw new ArgumentException(expr.ToString());
      }
    }
  }

  public class DelegateMappingMetadata<TYPE, DATA> where TYPE: class where DATA: class {
    public delegate void Delegator(DATA context);

    protected Dictionary<TYPE, Delegator> Metadata = new Dictionary<TYPE, Delegator>();

    public DelegateMappingMetadata() {
    }

    public void Add(TYPE typ, Delegator delegat) {
      Metadata.Add(typ, delegat);
    }

    public bool TryTranslate(TYPE typ, DATA context) {
      Delegator callDelegate = null;
      Metadata.TryGetValue(typ, out callDelegate);
      if (callDelegate != null) {
        callDelegate(context);
      }
      return (callDelegate != null);
    }
  }

  public class GenericMetadata<TYPE> : DelegateMappingMetadata<Type, TYPE> where TYPE: class {
    public void Translate(TYPE obj) {
      Type type = obj.GetType();
      bool translated = false;
      while (!translated) {
        translated = TryTranslate(type, obj);
        if (!translated) {
          type = type.BaseType;
          if (type == null) {
            throw new InvalidOperationException("No delegate for " + obj);
          }
        }
      }
    }
  }

  public class CiMetadata {
    protected GenericMetadata<CiSymbol> Symbols = new GenericMetadata<CiSymbol>();
    protected GenericMetadata<ICiStatement> Statemets = new GenericMetadata<ICiStatement>();
    public ExpressionMetadata Expressions = new ExpressionMetadata();
    public BinaryOperatorMetadata BinaryOperators = new BinaryOperatorMetadata();
    public UnaryOperatorMetadata UnaryOperators = new UnaryOperatorMetadata();

    public void AddSymbol(Type symbol, GenericMetadata<CiSymbol>.Delegator delegat) {
      Symbols.Add(symbol, delegat);
    }

    public void AddStatement(Type statemenent, GenericMetadata<ICiStatement>.Delegator delegat) {
      Statemets.Add(statemenent, delegat);
    }

    public void Translate(CiExpr expr) {
      Expressions.Translate(expr);
    }

    public void Translate(CiSymbol expr) {
      Symbols.Translate(expr);
    }

    public void Translate(ICiStatement expr) {
      Statemets.Translate(expr);
    }
  }
  //
  public delegate void WritePropertyAccessDelegate(CiPropertyAccess expr);
  public delegate void WriteMethodDelegate(CiMethodCall method);
  //
  public class LibraryMetadata {

    protected DelegateMappingMetadata<CiProperty, CiPropertyAccess> Properties = new DelegateMappingMetadata<CiProperty, CiPropertyAccess>();
    protected DelegateMappingMetadata<CiMethod, CiMethodCall> Methods = new DelegateMappingMetadata<CiMethod, CiMethodCall>();

    public LibraryMetadata() {
    }

    public void AddProperty(CiProperty prop, DelegateMappingMetadata<CiProperty, CiPropertyAccess>.Delegator del) {
      Properties.Add(prop, del);
    }

    public void AddMethod(CiMethod met, DelegateMappingMetadata<CiMethod, CiMethodCall>.Delegator del) {
      Methods.Add(met, del);
    }

    public bool Translate(CiPropertyAccess prop) {
      if (prop.Property != null) {
        return Properties.TryTranslate(prop.Property, prop);
      }
      else {
        return false;
      }
    }

    public bool Translate(CiMethodCall call) {
      if (call.Method != null) {
        return Methods.TryTranslate(call.Method, call);
      }
      else {
        return false;
      }
    }
  }
  #endregion
}