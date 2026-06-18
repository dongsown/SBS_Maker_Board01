import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

// Global variables
let scene, camera, renderer, controls;
let motorModel = null;
let isAnimating = false;
let animationId;

init();
animate();

function init() {
    const container = document.getElementById('canvas-container');

    // 1. Scene setup
    scene = new THREE.Scene();
    // Match the background color to the WPF UI
    scene.background = new THREE.Color(0xFAFAFA); 
    // Add a subtle grid to give a sense of space until the model is loaded
    const gridHelper = new THREE.GridHelper(10, 20, 0xCCCCCC, 0xEEEEEE);
    scene.add(gridHelper);

    // 2. Camera setup
    camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 0.1, 100);
    camera.position.set(3, 2, 3);

    // 3. Renderer setup
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(window.innerWidth, window.innerHeight);
    // Configure shadows and output encoding for PBR materials
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    container.appendChild(renderer.domElement);

    // 4. Lighting setup
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
    scene.add(ambientLight);

    const dirLight = new THREE.DirectionalLight(0xffffff, 1.5);
    dirLight.position.set(5, 5, 5);
    dirLight.castShadow = true;
    dirLight.shadow.camera.top = 2;
    dirLight.shadow.camera.bottom = - 2;
    dirLight.shadow.camera.left = - 2;
    dirLight.shadow.camera.right = 2;
    dirLight.shadow.camera.near = 0.1;
    dirLight.shadow.camera.far = 40;
    scene.add(dirLight);

    // 5. OrbitControls (for mouse interaction)
    controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.minDistance = 1;
    controls.maxDistance = 10;
    controls.target.set(0, 0.5, 0);

    // Handle window resize
    window.addEventListener('resize', onWindowResize);
}

// Global functions exposed to WPF C# via window object
window.loadModel = function(modelUrl) {
    document.getElementById('loading').innerText = "Loading 3D Model...";
    
    const loader = new GLTFLoader();
    loader.load(
        modelUrl,
        function (gltf) {
            // Remove previous model if exists
            if (motorModel) {
                scene.remove(motorModel);
            }

            motorModel = gltf.scene;
            
            // Enable shadows for the model
            motorModel.traverse(function (child) {
                if (child.isMesh) {
                    child.castShadow = true;
                    child.receiveShadow = true;
                }
            });

            // Center and scale the model automatically
            const box = new THREE.Box3().setFromObject(motorModel);
            const size = box.getSize(new THREE.Vector3());
            const center = box.getCenter(new THREE.Vector3());
            
            // Re-center
            motorModel.position.x += (motorModel.position.x - center.x);
            motorModel.position.y += (motorModel.position.y - center.y);
            motorModel.position.z += (motorModel.position.z - center.z);
            
            // Scale to fit roughly a 2x2x2 box
            const maxDim = Math.max(size.x, size.y, size.z);
            const targetSize = 2.0;
            if (maxDim > 0) {
                motorModel.scale.multiplyScalar(targetSize / maxDim);
            }

            // Adjust y position slightly up so it sits on the grid
            const newBox = new THREE.Box3().setFromObject(motorModel);
            motorModel.position.y += Math.abs(newBox.min.y);

            scene.add(motorModel);
            document.getElementById('loading').style.display = 'none';
        },
        function (xhr) {
            const percent = Math.round((xhr.loaded / xhr.total) * 100);
            document.getElementById('loading').innerText = `Loading... ${percent}%`;
        },
        function (error) {
            console.error('An error happened loading the model', error);
            document.getElementById('loading').innerText = "Error loading model!";
        }
    );
};

window.startMotorRotation = function() {
    isAnimating = true;
};

window.stopMotorRotation = function() {
    isAnimating = false;
};

function onWindowResize() {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
}

function animate() {
    animationId = requestAnimationFrame(animate);

    controls.update(); // required if controls.enableDamping is true

    // Rotate the model if animation is enabled
    if (isAnimating && motorModel) {
        // Rotate around Y axis
        motorModel.rotation.y += 0.05; 
    }

    renderer.render(scene, camera);
}

// Load the custom 3D model automatically at startup
window.loadModel('./oil_storage_medium_tank.glb');
