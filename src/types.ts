export type ToolCall = {
  name: string;
  args: Record<string, any>;
};

export type UnityAction = {
  type: "action";
  action: string;
  payload: any;
};

export type SceneObject = {
  id: string;
  type: string;
  position: { x: number; y: number; z: number };
};