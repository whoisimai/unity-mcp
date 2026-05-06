import { SceneObject } from "../types";

const scene: SceneObject[] = [];

export function addObject(obj: SceneObject) {
  scene.push(obj);
}

export function getScene() {
  return scene;
}

export function clearScene() {
  scene.length = 0;
}