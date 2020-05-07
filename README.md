# Save Play Mode Changes in Unity

EN :
Unity tool allowing changes made in play mode to be restored upon returning to edit mode.

KR :
플레이 모드를 종료하고 편집 모드로 전환시, 플레이 모드에서 수정했던 것들을 유지시켜주는 도구입니다.

## Usage

EN : 
Add the `SavePlayModeObject` component to the root of any hierarchies you'd like saved. That's it!
  
KR :
플레이 모드에서 변화를 저장하고 싶은 경우, `SavePlayModeObject` 컴포넌트를 변화가 저장되어야되는 오브젝트에 추가해줍니다. 참 쉽죠!

## Method

EN : 
Unlike other tools (such as PlayModePersist), this approximates the common trick of copy/pasting gameobjects from play mode to edit mode. 

We couldn't find a way to do this exactly as Unity does, so it serializes and deserializes gameobject hierarchies manually, mostly using UnityEngine.JSONUtility.

It's more of a hammer than a scalpel, but despite its drawbacks it can be a huge time saver so we're releasing it for anyone to use and improve.
**This tool is experimental. If something goes wrong, backups of your scenes are saved to a Backups folder on your desktop.**

KR :
이 에셋이 동작되는 방법은 플레이 모드에서 수정하고 종료시, 수정된 오브젝트를 복사/붙여넣기 방식으로 구현이 되어있다고 합니다.

유니티의 내부로직을 기반으로 동작하게하는것에는 한계가 이었습니다.

이 에셋은 달콤한 설탕이라기 보단 짭잘한 소금같은 느낌이지만, 개발 단계에서 시간을 절약할 수 있다면 정말 좋은 에셋이 될거라고 생각합니다.

**이 에셋은 실험적인 상태입니다. 만일 문제가 발생시 바탕화면에 씬 파일이 백업됩니다.**

### Advantages (장점)

EN : 
The main reason for this is to:
- Save newly created or destroyed Unity objects (GameObjects and Components)
- Saves changes made to serialized fields
- Maintains object references _to_ objects inside or outside the hierarchies you're saving.

KR :  
이 에셋을 사용시 장점 :  
- 오브젝트를 생성 or 삭제시 이를 저장해줍니다. (게임 오브젝트와 컴포넌트 포함)
- 인스펙터에 표기된 시리얼라이즈된 값을 변경했을 때, 이 값도 저장해줍니다.  
- 헤어라이키에서 계층 구조를 변경시 이 또한 저장해줍니다.  


### Disadvantages (단점)

EN : 
It's a brute force sort of solution. This means:
- You can't currently make exceptions to what's saved
- It'll break any references _from outside the_ list of things to save _into_ the list of things to save
- Makes lots of source control changes
- Breaks prefab connections
- Deselects and closes a previously selected and expanded hierarchy (not investigated)
- Can't save anything marked static, since static meshes are combined and don’t have asset files
- We've not found one in a while, but some components may not save properly

KR :
- 현재 저장시키는 부분에 예외를 둘 수 없습니다.
- It'll break any references _from outside the_ list of things to save _into_ the list of things to save
- 변경사항이 너무 많이 발생시 문제가 발생합니다.
- Breaks prefab connections
- 저장할 오브젝트를 계층 구조를 형성했을 때 해당 계층 구조에 `SavePlayModeObject`를 두지 않으면 문제가 발생하게 됩니다.
- Can't save anything marked static, since static meshes are combined and don’t have asset files
- 저장할 요소를 찾지못한 경우, 재대로 저장되 되지 않을 수 있습니다.


## How it works

EN :
The SavePlayModeChangesChecker class finds all references to `SavePlayModeObject` components on exiting the game. It serializes the entire hierarchy for those objects, and on entering play mode deletes the old hierarchies and creates the new ones.

KR :
`SavePlayModeChangesChecker` 클래스는 게임 종료 시 'SavePlayModeObject' 구성 요소에 대한 모든 참조를 찾습니다.  
전체 계층을 직렬화하고 플레이 모드로 전환하면 이전 계층이 삭제되고 새 계층이 생성됩니다.

## Other issues

EN :
- Undoing restored changes can break object references
- Small wait time when exiting play mode if something requires restoring
- Only scenes that are open in edit mode can be restored, and changing scenes in game will prevent the unloaded scenes being saved

KR :
- 복원된 변경 사항을 실행 취소하면 개체 참조가 손상될 수 있습니다.
- 복원이 필요한 경우 재생 모드를 종료할 때 대기 시간이 짧습니다.
- 편집 모드에서 열린 장면만 복원할 수 있으며, 게임에서 장면을 변경하면 언로드된 장면이 저장되지 않습니다.

## License

SavePlayModeChanges is released under the MIT license. Although we don't require attribution, we'd love to hear feedback, and Twitter follows ([@inklestudios](https://twitter.com/inklestudios)) are always appreciated!
