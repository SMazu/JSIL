﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JSIL.Ast;
using JSIL.Internal;
using Mono.Cecil;

namespace JSIL.Transforms {
    public class TypeExpressionCacher : JSAstVisitor {
        public readonly Dictionary<GenericTypeIdentifier, JSCachedType> CachedTypes;
        public readonly TypeReference ThisType;
        private int NextToken = 0;

        public TypeExpressionCacher (TypeReference thisType) {
            ThisType = thisType;
            CachedTypes = new Dictionary<GenericTypeIdentifier, JSCachedType>();
        }

        private JSCachedType MakeCachedType (TypeReference type) {
            string token;
            
            if (TypeUtil.TypesAreEqual(ThisType, type, true))
                token = "$Tthis";
            else
                token = String.Format("$T{0:X2}", NextToken++);

            return new JSCachedType(type, token);
        }

        private JSCachedType GetCachedType (TypeReference type) {
            if (!IsCacheable(type))
                return null;

            var resolved = type.Resolve();
            if (resolved == null)
                return null;

            TypeDefinition[] arguments;
            var git = type as GenericInstanceType;

            if (git != null) {
                arguments = (from a in git.GenericArguments select a.Resolve()).ToArray();
            } else {
                arguments = new TypeDefinition[0];
            }

            var identifier = new GenericTypeIdentifier(resolved, arguments);
            JSCachedType result;
            if (!CachedTypes.TryGetValue(identifier, out result))
                CachedTypes.Add(identifier, result = MakeCachedType(type));

            return result;
        }

        public static bool IsCacheable (TypeReference type) {
            if (TypeUtil.ContainsGenericParameter(type))
                return false;

            if (TypeUtil.IsOpenType(type))
                return false;

            return true;
        }

        public void VisitNode (JSType type) {
            var ct = GetCachedType(type.Type);

            if (ct != null) {
                ParentNode.ReplaceChild(type, ct);
                VisitReplacement(ct);
            } else {
                VisitChildren(type);
            }
        }

        public void VisitNode (JSTypeReference tr) {
            var ct = GetCachedType(tr.Type);

            if (ct != null) {
                ParentNode.ReplaceChild(tr, ct);
                VisitReplacement(ct);
            } else {
                VisitChildren(tr);
            }
        }

        public void VisitNode (JSCachedType ct) {
            VisitChildren(ct);
        }

        public JSCachedType[] CacheTypesForFunction (JSFunctionExpression function) {
            var currentKeys = new HashSet<GenericTypeIdentifier>(CachedTypes.Keys);

            Visit(function);

            var newKeys = new HashSet<GenericTypeIdentifier>(CachedTypes.Keys);
            newKeys.ExceptWith(currentKeys);

            return (from k in newKeys 
                    let ct = CachedTypes[k]
                    orderby ct.Token
                    select ct).ToArray();
        }
    }
}
