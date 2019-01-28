
#version 330 core
out vec4 FragColor;

in VS_OUT {
    vec3 FragPos;
    vec3 Normal;
    vec2 TexCoords;
    vec4 FragPosLightSpace;
} fs_in;

uniform sampler2D diffuseTexture;
uniform sampler2D shadowMap;
uniform samplerCube depthMap;

uniform vec3 dirLightPos;
uniform vec3 pointLightPos;
uniform vec3 spotLightPos;
uniform vec3 spotLightDir;
uniform vec3 viewPos;

uniform float far_plane;
uniform bool shadowsDir;
uniform bool shadowsPoint;
uniform bool shadowsSpot;

float ShadowCalculation(vec4 fragPosLightSpace)
{
    // perform perspective divide
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
    // transform to [0,1] range
    projCoords = projCoords * 0.5 + 0.5;
    // get closest depth value from light's perspective (using [0,1] range fragPosLight as coords)
    float closestDepth = texture(shadowMap, projCoords.xy).r;
    // get depth of current fragment from light's perspective
    float currentDepth = projCoords.z;
    // calculate bias (based on depth map resolution and slope)
    vec3 normal = normalize(fs_in.Normal);
    vec3 lightDir = normalize(dirLightPos - fs_in.FragPos);
    float bias = max(0.05 * (1.0 - dot(normal, lightDir)), 0.005);
    // check whether current frag pos is in shadow
    // float shadow = currentDepth - bias > closestDepth  ? 1.0 : 0.0;
    // PCF
    float shadow = 0.0;
    vec2 texelSize = 1.0 / textureSize(shadowMap, 0);
    for(int x = -1; x <= 1; ++x)
    {
        for(int y = -1; y <= 1; ++y)
        {
            float pcfDepth = texture(shadowMap, projCoords.xy + vec2(x, y) * texelSize).r;
            shadow += currentDepth - bias > pcfDepth  ? 1.0 : 0.0;
        }
    }
    shadow /= 9.0;

    // keep the shadow at 0.0 when outside the far_plane region of the light's frustum.
    if(projCoords.z > 1.0)
        shadow = 0.0;

    return shadow;
}

float PointShadowCalculation(vec3 fragPos)
{
    // get vector between fragment position and light position
    vec3 fragToLight = fragPos - pointLightPos;
    // ise the fragment to light vector to sample from the depth map
    float closestDepth = texture(depthMap, fragToLight).r;
    // it is currently in linear range between [0,1], let's re-transform it back to original depth value
    closestDepth *= far_plane;
    // now get current linear depth as the length between the fragment and light position
    float currentDepth = length(fragToLight);
    // test for shadows
    float bias = 0.05; // we use a much larger bias since depth is now in [near_plane, far_plane] range
    float shadow = currentDepth -  bias > closestDepth ? 1.0 : 0.0;
    // display closestDepth as debug (to visualize depth cubemap)
    // FragColor = vec4(vec3(closestDepth / far_plane), 1.0);

    return shadow;
}

float SpotShadowCalculation(vec3 fragPos)
{
    // get vector between fragment position and light position
    vec3 fragToLight = fragPos - pointLightPos;
    // ise the fragment to light vector to sample from the depth map
    float closestDepth = texture(depthMap, fragToLight).r;
    // it is currently in linear range between [0,1], let's re-transform it back to original depth value
    closestDepth *= far_plane;
    // now get current linear depth as the length between the fragment and light position
    float currentDepth = length(fragToLight);
    // test for shadows
    float bias = 0.05; // we use a much larger bias since depth is now in [near_plane, far_plane] range
    float shadow = currentDepth -  bias > closestDepth ? 1.0 : 0.0;
    // display closestDepth as debug (to visualize depth cubemap)
    // FragColor = vec4(vec3(closestDepth / far_plane), 1.0);

    return shadow;
}

void main()
{
    vec3 color = texture(diffuseTexture, fs_in.TexCoords).rgb;
    vec3 normal = normalize(fs_in.Normal);
    vec3 lightColor = vec3(0.3);

    // dir ambient
    vec3 dirAmbient = 0.1 * color;
    // dir diffuse
    vec3 dirDir = normalize(dirLightPos - fs_in.FragPos);
    float dirDiff = max(dot(dirDir, normal), 0.0);
    vec3 dirDiffuse = dirDiff * lightColor;
    // dir specular
    vec3 dirViewDir = normalize(viewPos - fs_in.FragPos);
    vec3 dirReflectDir = reflect(-dirDir, normal);
    float dirSpec = 0.0;
    vec3 dirHalfwayDir = normalize(dirDir + dirViewDir);
    dirSpec = pow(max(dot(normal, dirHalfwayDir), 0.0), 64.0);
    vec3 dirSpecular = dirSpec * lightColor;

    // calculate dir shadow
    float dirShadow = shadowsDir ? ShadowCalculation(fs_in.FragPosLightSpace) : 0.0;

    vec3 dirLighting = (dirAmbient + (1.0 - dirShadow) * (dirDiffuse + dirSpecular)) * color;


    // point ambient
    vec3 pointAmbient = 0.2 * color;
    // point diffuse
    vec3 pointDir = normalize(pointLightPos - fs_in.FragPos);
    float pointDiff = max(dot(pointDir, normal), 0.0);
    vec3 pointDiffuse = pointDiff * lightColor;
    // point specular
    vec3 pointViewDir = normalize(viewPos - fs_in.FragPos);
    vec3 pointReflectDir = reflect(-pointDir, normal);
    float pointSpec = 0.0;
    vec3 pointHalfwayDir = normalize(pointDir + pointViewDir);
    pointSpec = pow(max(dot(normal, pointHalfwayDir), 0.0), 64.0);
    vec3 pointSpecular = pointSpec * lightColor;

    // calculate point shadow
    float pointShadow = shadowsPoint ? PointShadowCalculation(fs_in.FragPos) : 0.0;

    vec3 pointLighting = (pointAmbient + (1.0 - pointShadow) * (pointDiffuse + pointSpecular)) * color;


    // spot ambient
    vec3 spotAmbient = 0.005 * color;
    // spot diffuse
    vec3 spotDir = normalize(spotLightPos - fs_in.FragPos);
    float spotDiff = max(dot(spotDir, normal), 0.0);
    vec3 spotDiffuse = spotDiff * lightColor;
    // spot specular
    vec3 spotViewDir = normalize(viewPos - fs_in.FragPos);
    vec3 spotReflectDir = reflect(-spotDir, normal);
    float spotSpec = 0.0;
    vec3 spotHalfwayDir = normalize(spotDir + spotViewDir);
    spotSpec = pow(max(dot(normal, spotHalfwayDir), 0.0), 64.0);
    vec3 spotSpecular = spotSpec * lightColor;

    // spotlight intensity
    float theta = dot(spotDir, normalize(-spotLightDir));
    float epsilon = 30.0f - 45.0f; // cutOff - outerCutOff
    float intensity = clamp((theta - 45.0f) / epsilon, 0.0, 1.0);

    spotAmbient *= intensity;
    spotDiffuse *= intensity;
    spotSpecular *= intensity;

    // calculate spot shadow
    float spotShadow = shadowsSpot ? PointShadowCalculation(fs_in.FragPos) : 0.0;

    vec3 spotLighting = (spotAmbient + (1.0 - spotShadow) * (spotDiffuse + spotSpecular)) * color;


    FragColor = vec4(dirLighting + pointLighting + spotLighting, 1.0);
}

