using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace PlayModeSaver
{
    public class SavePlayModeObject : MonoBehaviour
    {
        public bool IsValid() => enabled && !AnyAncestorHasThisComponent() && !AnyDescendentIsStatic();

        public bool AnyAncestorHasThisComponent()
        {
            var ancestors = GetAllAncestors(transform);
            
            return ancestors.Any(ancestor =>
                ancestor.GetComponent<SavePlayModeObject>() && ancestor.GetComponent<SavePlayModeObject>().enabled);
        }

        public bool AnyDescendentIsStatic()
        {
            var descendents = GetAllDescendents(transform);
            return descendents.Any(descendent => descendent.gameObject.isStatic);
        }

        private IEnumerable<Transform> GetAllAncestors(Transform _transform)
        {
            var parents = new List<Transform>();
            while (_transform.parent != null)
            {
                _transform = _transform.parent;
                parents.Add(_transform);
            }

            return parents;
        }

        private List<Transform> GetAllDescendents(Transform current, List<Transform> transforms = null)
        {
            if (transforms == null) transforms = new List<Transform>();
            transforms.Add(current);
            
            for (int i = 0; i < current.childCount; ++i) 
                GetAllDescendents(current.GetChild(i), transforms);
            
            return transforms;
        }
    }
}
